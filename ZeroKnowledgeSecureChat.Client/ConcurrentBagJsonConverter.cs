using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZeroKnowledgeSecureChat;

public class ConcurrentBagJsonConverter<T> : JsonConverter<ConcurrentBag<T>>
{
    public override ConcurrentBag<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Deserialize JSON array into a temporary list
        List<T>? items = JsonSerializer.Deserialize<List<T>>(ref reader, options);
        return [.. items ?? []];
    }

    public override void Write(Utf8JsonWriter writer, ConcurrentBag<T> value, JsonSerializerOptions options)
    {
        // Serialize as a normal array
        JsonSerializer.Serialize(writer, value.ToArray(), options);
    }
}