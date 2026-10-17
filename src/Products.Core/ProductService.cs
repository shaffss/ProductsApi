using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Products.Core.Abstractions;
using Products.Core.Entities;

namespace Products.Core;

public sealed class ProductService(IProductRepository repository, IProductCache cache, CurrencyCatalog currencies)
{
    public const int NameMaxLength = 200;
    public const decimal MaxPrice = 9_999_999_999_999_999.99m; // decimal(18,2)
    public const int IdempotencyKeyMaxLength = 100;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private static readonly KeyValuePair<string, object?> CacheHit = new("cache.result", "hit");
    private static readonly KeyValuePair<string, object?> CacheMiss = new("cache.result", "miss");

    // Trwające odczyty z bazy przy braku w cache - po jednym na produkt
    private readonly ConcurrentDictionary<Guid, Lazy<Task<Product?>>> _loads = new();

    // Cache-aside: najpierw cache; przy braku baza i zapis do cache (z TTL)
    public async Task<ProductLookup> GetAsync(Guid id, CancellationToken ct)
    {
        if (!cache.IsEnabled)
            return new(await repository.GetAsync(id, ct), FromCache: false);

        if (await cache.GetAsync(id) is { } cached)
        {
            ProductsTelemetry.CacheLookups.Add(1, CacheHit);
            return new(cached, FromCache: true);
        }

        // Cache stampede: równoczesne żądania o produkt, którego nie ma w cache (np. popularny tuż po zmianie ceny),
        // czekają na jeden odczyt z bazy zamiast wysyłać każde własne zapytanie
        ProductsTelemetry.CacheLookups.Add(1, CacheMiss);
        var load = _loads.GetOrAdd(id, static (id, service) => new(() => service.LoadAsync(id)), this);
        return new(await load.Value.WaitAsync(ct), FromCache: false);
    }

    // Stronicowanie skip/take (offset) - prosto i ze skokiem do dowolnej strony; lista bez cache
    public async Task<Result<ProductPage>> ListAsync(int? skip, int? take, CancellationToken ct)
    {
        Dictionary<string, string[]>? errors = null;
        if (skip < 0)
            AddError(ref errors, "skip", "Skip must not be negative.");
        if (take is < 1 or > MaxPageSize)
            AddError(ref errors, "take", $"Take must be between 1 and {MaxPageSize}.");

        if (errors is not null)
            return Result<ProductPage>.Invalid(errors);
        return await repository.ListAsync(skip ?? 0, take ?? DefaultPageSize, ct);
    }

    // Value == null bez błędów: klucz idempotencji był już użyty z inną treścią żądania
    public async Task<Result<CreatedProduct>> CreateAsync(
        string? name, decimal? price, string? currency, string? idempotencyKey, CancellationToken ct)
    {
        Dictionary<string, string[]>? errors = null;

        name = name?.Trim();
        if (string.IsNullOrEmpty(name))
            AddError(ref errors, "name", "Name is required.");
        else if (name.Length > NameMaxLength)
            AddError(ref errors, "name", $"Name must not exceed {NameMaxLength} characters.");

        ValidatePrice(price, ref errors);
        var (currencyCode, currencyError) = await CheckCurrencyAsync(currency, ct);
        if (currencyError is not null)
            AddError(ref errors, "currency", currencyError);

        // varchar w bazie - tylko widoczne znaki ASCII (bez spacji)
        if (idempotencyKey is { Length: 0 or > IdempotencyKeyMaxLength } || idempotencyKey.AsSpan().ContainsAnyExceptInRange('!', '~'))
            AddError(ref errors, "Idempotency-Key", $"Idempotency-Key must have 1-{IdempotencyKeyMaxLength} visible ASCII characters.");

        if (errors is not null)
            return Result<CreatedProduct>.Invalid(errors);
        if (idempotencyKey is null)
            return new CreatedProduct(await repository.CreateAsync(name!, price!.Value, currencyCode!, ct), Replayed: false);
        return await CreateIdempotentAsync(name!, price!.Value, currencyCode!, idempotencyKey, ct);
    }

    // Value == null bez błędów: produkt nie istnieje; Value.Changed == false: nic nie zapisano -
    // cena i waluta bez zmian albo Value.VersionConflict (produkt ma inną wersję niż expectedVersion)
    public async Task<Result<PriceUpdate>> ChangePriceAsync(
        Guid id, decimal? price, string? currency, int? expectedVersion, CancellationToken ct)
    {
        Dictionary<string, string[]>? errors = null;

        ValidatePrice(price, ref errors);
        var (currencyCode, currencyError) = await CheckCurrencyAsync(currency, ct);
        if (currencyError is not null)
            AddError(ref errors, "currency", currencyError);

        if (errors is not null)
            return Result<PriceUpdate>.Invalid(errors);

        var update = await repository.ChangePriceAsync(id, price!.Value, currencyCode!, expectedVersion, ct);
        // po zatwierdzeniu w bazie - kolejny odczyt odtworzy cache z aktualnych danych.
        // Nieudane usunięcie (Redis niedostępny) ponowi konsument zdarzenia ProductPriceChanged.
        if (update is { Changed: true })
            await cache.TryRemoveAsync(id);
        return update;
    }

    // Usuwanie produktów wyłączone (decyzja biznesowa) - kod zakomentowany, nie usunięty
    // // false - produkt nie istnieje. Cache czyszczony po zatwierdzeniu w bazie, jak przy zmianie ceny.
    // public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    // {
    //     var deleted = await repository.DeleteAsync(id, ct);
    //     if (deleted)
    //         await cache.TryRemoveAsync(id);
    //     return deleted;
    // }

    // Wynik współdzielony przez czekające żądania - bez ich CancellationToken: anulowanie jednego nie przerywa reszty
    private async Task<Product?> LoadAsync(Guid id)
    {
        try
        {
            var product = await repository.GetAsync(id, CancellationToken.None);
            if (product is not null)
                await cache.SetAsync(product);
            return product;
        }
        finally
        {
            _loads.TryRemove(id, out _);
        }
    }

    // Idempotency-Key: ponowione żądanie (np. po utracie odpowiedzi) dostaje produkt utworzony za pierwszym razem
    // zamiast tworzyć drugi. Ten sam klucz z inną treścią to błąd klienta (null).
    private async Task<CreatedProduct?> CreateIdempotentAsync(string name, decimal price, string currency, string key, CancellationToken ct)
    {
        // skrót znormalizowanej treści - np. ceny 10 i 10.00 to to samo żądanie
        var requestHash = SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"{name}\n{price:0.00}\n{currency}")));

        while (true)
        {
            var product = await repository.TryCreateAsync(
                name, price, currency, new IdempotencyKey { Key = key, RequestHash = requestHash }, ct);
            if (product is not null)
                return new(product, Replayed: false);

            if (await repository.GetIdempotencyKeyAsync(key, ct) is { } existing)
                return existing.RequestHash.AsSpan().SequenceEqual(requestHash) ? new(existing.Product!, Replayed: true) : null;
            // klucz usunięty między zapytaniami (retencja) - kolejna próba utworzenia
        }
    }

    private static void ValidatePrice(decimal? price, ref Dictionary<string, string[]>? errors)
    {
        if (price is not { } value)
            AddError(ref errors, "price", "Price is required.");
        else if (value <= 0)
            AddError(ref errors, "price", "Price must be greater than zero.");
        else if (value > MaxPrice)
            AddError(ref errors, "price", $"Price must not exceed {MaxPrice}.");
        else if (decimal.Round(value, 2) != value) // baza zaokrągliłaby po cichu
            AddError(ref errors, "price", "Price can have at most 2 decimal places.");
    }

    // Kod znormalizowany do wielkich liter (np. "pln" -> "PLN") albo komunikat błędu
    private async ValueTask<(string? Code, string? Error)> CheckCurrencyAsync(string? currency, CancellationToken ct)
    {
        var code = currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
            return (null, "Currency is required.");
        if (!await currencies.IsActiveAsync(code, ct))
            return (null, $"Currency '{code}' is not supported.");
        return (code, null);
    }

    // Słownik tworzony dopiero przy pierwszym błędzie - poprawne żądania nic nie alokują
    private static void AddError(ref Dictionary<string, string[]>? errors, string field, string message) =>
        (errors ??= new())[field] = [message];
}
