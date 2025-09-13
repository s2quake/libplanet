using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.Descriptors;

internal sealed class ArrayJsonDescriptor : JsonConverter<IList>
{
    public override bool CanConvert(Type typeToConvert) => IsArray(typeToConvert);

    private static bool IsArray(Type type) => IsArray(type, out _);

    private static bool IsArray(Type type, [MaybeNullWhen(false)] out Type elementType)
    {
        if (typeof(Array).IsAssignableFrom(type))
        {
            elementType = type.GetElementType()!;
            return true;
        }

        elementType = null;
        return false;
    }

    public override IList? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, IList value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var item in value)
        {
            if (item is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                JsonSerializer.Serialize(writer, item, item.GetType(), options);
            }
        }

        writer.WriteEndArray();
    }
}
