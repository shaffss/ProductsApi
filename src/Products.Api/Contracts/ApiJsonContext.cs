using System.Text.Json;
using System.Text.Json.Serialization;

namespace Products.Api.Contracts;

// Serializacja generowana w czasie kompilacji - bez refleksji przy każdym żądaniu
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(CreateProductRequest))]
[JsonSerializable(typeof(ChangePriceRequest))]
[JsonSerializable(typeof(ProductResponse))]
[JsonSerializable(typeof(HealthResponse))]
internal sealed partial class ApiJsonContext : JsonSerializerContext;
