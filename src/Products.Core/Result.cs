using System.Diagnostics.CodeAnalysis;

namespace Products.Core;

// Wynik operacji: wartość albo błędy walidacji (pole -> komunikaty). Bez wyjątków - tańsze na gorącej ścieżce.
// Value == null bez błędów - wynik szczególny opisany przy operacji (np. brak zasobu).
public readonly record struct Result<T>(T? Value, Dictionary<string, string[]>? Errors) where T : class
{
    public static implicit operator Result<T>(T? value) => new(value, null);

    [SuppressMessage("Design", "CA1000", Justification = "Fabryka czytelna w miejscu użycia: Result<Product>.Invalid(errors)")]
    public static Result<T> Invalid(Dictionary<string, string[]> errors) => new(null, errors);
}
