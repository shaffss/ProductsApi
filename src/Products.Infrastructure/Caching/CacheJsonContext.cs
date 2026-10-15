using System.Text.Json;
using System.Text.Json.Serialization;
using Products.Core.Entities;

namespace Products.Infrastructure.Caching;

// Serializacja generowana w czasie kompilacji - bez refleksji przy każdym odczycie z cache
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(Product))]
internal sealed partial class CacheJsonContext : JsonSerializerContext;
