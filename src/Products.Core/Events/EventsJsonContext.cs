using System.Text.Json;
using System.Text.Json.Serialization;

namespace Products.Core.Events;

// Serializacja generowana w czasie kompilacji - bez refleksji w runtime
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ProductPriceChanged))]
internal sealed partial class EventsJsonContext : JsonSerializerContext;
