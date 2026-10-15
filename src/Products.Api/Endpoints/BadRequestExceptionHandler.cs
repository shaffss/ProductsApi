using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace Products.Api.Endpoints;

// Niepoprawne żądanie (zły JSON, brak body) -> 400 ProblemDetails ze wskazaniem pola.
// Bez tego: w Development 500 (wyjątek trafia do ogólnego handlera), w Production 400 bez szczegółów.
// Komunikat nie zawiera nazw typów .NET z oryginalnego wyjątku.
internal sealed class BadRequestExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not BadHttpRequestException badRequest)
            return false;

        context.Response.StatusCode = badRequest.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails =
            {
                Status = badRequest.StatusCode,
                Title = "Invalid request",
                Detail = badRequest.InnerException is JsonException { Path: { } path }
                    ? $"Invalid JSON value at '{path}'."
                    : "The request body is missing or is not valid JSON.",
            },
        });
    }
}
