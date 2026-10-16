using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Confluent.Kafka;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Products.Api.Contracts;
using Products.Core.Events;
using Products.IntegrationTests.Fixtures;
using Products.Persistence;
using StackExchange.Redis;

namespace Products.IntegrationTests;

// Cały przepływ przez HTTP dla każdego wariantu: API -> baza -> Redis -> outbox -> Kafka -> konsument
public sealed class ApiEndToEndTests(SqlServerFixture sqlServer, PostgresFixture postgres, RedisFixture redis, KafkaFixture kafka)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<OrmKind, DatabaseEngine> Variants => new()
    {
        { OrmKind.EfCore, DatabaseEngine.SqlServer },
        { OrmKind.EfCore, DatabaseEngine.Postgres },
        { OrmKind.Dapper, DatabaseEngine.SqlServer },
        { OrmKind.Dapper, DatabaseEngine.Postgres },
    };

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task Price_change_flows_through_cache_outbox_and_kafka(OrmKind orm, DatabaseEngine engine)
    {
        await using var factory = await CreateFactoryAsync(orm, engine);
        var client = factory.CreateClient();

        var health = await client.GetFromJsonAsync<HealthResponse>("/health/ready", Ct);
        Assert.Equal(($"{orm} + {engine}", "Healthy"), (health?.Variant, health?.Status));
        using (var live = await client.GetAsync("/health/live", Ct))
            Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        // utworzenie
        var created = await client.PostAsJsonAsync("/api/products", new { name = "Czujnik temperatury", price = 149.99m, currency = "PLN" }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var product = (await created.Content.ReadFromJsonAsync<ProductResponse>(Ct))!;
        Assert.Equal($"/api/products/{product.Id}", created.Headers.Location?.ToString());
        Assert.Equal("\"1\"", created.Headers.ETag?.Tag);

        // niepoprawne id - niepoprawne dane wejściowe (400), a nie brak zasobu (404)
        using (var invalidGet = await client.GetAsync("/api/products/not-a-guid", Ct))
            Assert.Equal(HttpStatusCode.BadRequest, invalidGet.StatusCode);
        using (var invalidPut = await client.PutAsJsonAsync("/api/products/not-a-guid/price", new { price = 1m, currency = "PLN" }, Ct))
            Assert.Contains("\"id\":", await invalidPut.Content.ReadAsStringAsync(Ct));

        // cache-aside: pierwszy odczyt z bazy, drugi z Redisa
        Assert.Equal("MISS", await GetCacheStatusAsync(client, product.Id));
        Assert.Equal("HIT", await GetCacheStatusAsync(client, product.Id));

        // zmiana ceny warunkowa (If-Match z ETag) w śladzie o znanym trace-id: nowy stan i wersja, wpis cache unieważniony
        var traceId = ActivityTraceId.CreateRandom().ToHexString();
        using var changed = await PutPriceAsync(client, product.Id, new { price = 129.99m, currency = "EUR" }, ifMatch: "\"1\"",
            traceParent: $"00-{traceId}-{ActivitySpanId.CreateRandom().ToHexString()}-01");
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var afterChange = await changed.Content.ReadFromJsonAsync<ProductResponse>(Ct);
        Assert.Equal((129.99m, "EUR", "\"2\""), (afterChange?.Price, afterChange?.Currency, changed.Headers.ETag?.Tag));
        Assert.Equal("MISS", await GetCacheStatusAsync(client, product.Id));

        // ponownie z ETag sprzed zmiany - produkt zmienił się w międzyczasie: 412 i nic nie zapisane
        using (var stale = await PutPriceAsync(client, product.Id, new { price = 1m, currency = "PLN" }, ifMatch: "\"1\""))
            Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        using (var malformed = await PutPriceAsync(client, product.Id, new { price = 1m, currency = "PLN" }, ifMatch: "2"))
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);

        // zdarzenie: outbox -> Kafka -> konsument (ProcessedEvents) w rozsądnym czasie
        var db = new TestDatabase(factory.Services.GetRequiredService<DbDataSource>(), factory.Services.GetRequiredService<SqlDialect>());
        var outbox = Assert.Single(await db.GetOutboxAsync(product.Id));
        var priceChanged = ProductPriceChanged.FromJson(outbox.Payload)!;
        Assert.Equal((149.99m, 129.99m, "EUR", "PLN"), (priceChanged.OldPrice, priceChanged.NewPrice, priceChanged.Currency, priceChanged.OldCurrency));

        await WaitUntilAsync(() => db.IsEventProcessedAsync(priceChanged.EventId), TimeSpan.FromSeconds(60));
        // konsument bywa szybszy niż zatwierdzenie oznaczenia w outboxie (at-least-once) - czekamy na oba stany
        await WaitUntilAsync(async () => (await db.GetOutboxAsync(product.Id))[0].ProcessedAtUtc is not null, TimeSpan.FromSeconds(30));

        // wiadomość w Kafce: typ zdarzenia z wersją i ślad żądania HTTP (ten sam trace-id)
        var headers = ReadKafkaHeaders(product.Id);
        Assert.Equal(ProductPriceChanged.EventType, Encoding.UTF8.GetString(headers.GetLastBytes("event-type")));
        Assert.StartsWith($"00-{traceId}-", Encoding.UTF8.GetString(headers.GetLastBytes("traceparent")));

        // konsument też usuwa wpis cache (drugie unieważnienie) - wstawiony ręcznie stary wpis znika po kolejnym zdarzeniu
        var redisDb = factory.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        using (await PutPriceAsync(client, product.Id, new { price = 119.99m, currency = "EUR" }))
        {
        }
        await redisDb.StringSetAsync($"product:{product.Id}", """{"stale":true}""");
        var secondEvent = ProductPriceChanged.FromJson((await db.GetOutboxAsync(product.Id))[1].Payload)!;
        await WaitUntilAsync(() => db.IsEventProcessedAsync(secondEvent.EventId), TimeSpan.FromSeconds(60));
        Assert.False(await redisDb.KeyExistsAsync($"product:{product.Id}"));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task Create_with_idempotency_key_is_safe_to_retry(OrmKind orm, DatabaseEngine engine)
    {
        await using var factory = await CreateFactoryAsync(orm, engine);
        var client = factory.CreateClient();
        var key = Guid.NewGuid().ToString();

        using var first = await PostWithKeyAsync(client, key, new { name = "Czujnik CO2", price = 299m, currency = "PLN" });
        // ponowienie po utracie odpowiedzi - ta sama treść (inaczej zapisana)
        using var retry = await PostWithKeyAsync(client, key, new { name = " Czujnik CO2 ", price = 299.00m, currency = "pln" });
        // ten sam klucz z inną treścią - błąd klienta
        using var reused = await PostWithKeyAsync(client, key, new { name = "Czujnik CO2", price = 199m, currency = "PLN" });

        Assert.Equal(
            (HttpStatusCode.Created, HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity),
            (first.StatusCode, retry.StatusCode, reused.StatusCode));
        Assert.False(first.Headers.Contains("Idempotent-Replayed"));
        Assert.Contains("\"price\":299.00", await first.Content.ReadAsStringAsync(Ct)); // jak w bazie, w każdym wariancie
        Assert.Equal("true", retry.Headers.GetValues("Idempotent-Replayed").Single());
        var (original, replayed) = (await first.Content.ReadFromJsonAsync<ProductResponse>(Ct), await retry.Content.ReadFromJsonAsync<ProductResponse>(Ct));
        Assert.Equal(original?.Id, replayed?.Id);
        Assert.Equal(first.Headers.Location, retry.Headers.Location);
    }

    // Każdy test na własnej, pustej bazie - schemat zakłada API przy starcie, jak w compose: warianty EF z migracji,
    // warianty Dappera ze skryptów. Telemetria wyłączona (appsettings.Development.json wskazuje Aspire Dashboard).
    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync(OrmKind orm, DatabaseEngine engine)
    {
        DatabaseFixture database = engine == DatabaseEngine.SqlServer ? sqlServer : postgres;
        var connectionString = await database.CreateDatabaseAsync(SchemaSource.None);
        var schema = orm == OrmKind.EfCore ? SchemaSource.Migrations : SchemaSource.Scripts;
        return new WebApplicationFactory<Program>().WithWebHostBuilder(host => host
            .UseEnvironment("Development")
            .UseSetting("Database:Schema", schema.ToString())
            .UseSetting("Persistence:Orm", orm.ToString())
            .UseSetting("Persistence:Database", engine.ToString())
            .UseSetting($"ConnectionStrings:{engine}", connectionString)
            .UseSetting("ConnectionStrings:Redis", redis.ConnectionString)
            .UseSetting("Kafka:BootstrapServers", kafka.BootstrapServers)
            .UseSetting("Kafka:ConsumerGroup", $"e2e-{orm}-{engine}-{Guid.NewGuid():N}")
            .UseSetting("Kafka:OutboxPollInterval", "00:00:00.200")
            .UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", ""));
    }

    private static async Task<string?> GetCacheStatusAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"/api/products/{id}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.TryGetValues("X-Cache", out var values) ? values.Single() : null;
    }

    private static Task<HttpResponseMessage> PutPriceAsync(
        HttpClient client, Guid id, object body, string? ifMatch = null, string? traceParent = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/products/{id}/price") { Content = JsonContent.Create(body) };
        if (ifMatch is not null)
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        if (traceParent is not null)
            request.Headers.Add("traceparent", traceParent);
        return client.SendAsync(request, Ct);
    }

    // wartość wg specyfikacji IETF - w cudzysłowie
    private static Task<HttpResponseMessage> PostWithKeyAsync(HttpClient client, string key, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/products") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", $"\"{key}\"");
        return client.SendAsync(request, Ct);
    }

    // Nagłówki wiadomości produktu - osobna grupa konsumentów czyta topic od początku
    private Headers ReadKafkaHeaders(Guid productId)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = kafka.BootstrapServers,
            GroupId = $"e2e-headers-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
        }).Build();
        consumer.Subscribe(ProductPriceChanged.Topic);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
            if (consumer.Consume(TimeSpan.FromSeconds(1)) is { } result && result.Message.Key == productId.ToString())
                return result.Message.Headers;
        throw new TimeoutException($"No Kafka message for product {productId}");
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Condition not met within {timeout}");
            await Task.Delay(200, Ct);
        }
    }
}
