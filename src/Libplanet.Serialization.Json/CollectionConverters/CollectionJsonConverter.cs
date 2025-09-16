using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.CollectionConverters;

internal abstract class CollectionJsonConverter : JsonConverter<IEnumerable>
{
    protected abstract Type GenericTypeDefinition { get; }

    protected abstract bool IsDictionary { get; }

    public override IEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Expect(JsonTokenType.StartArray);
        var elementType = GetElementType(typeToConvert);
        var listInstance = CreateListInstance(elementType);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var value = JsonSerializer.Deserialize(ref reader, elementType, options);
            listInstance.Add(value);
        }

        reader.Expect(JsonTokenType.EndArray);
        return CreateInstance(typeToConvert, elementType, listInstance);
    }

    public override void Write(Utf8JsonWriter writer, IEnumerable value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        var enumerator = value.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var current = enumerator.Current;
            if (current is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                JsonSerializer.Serialize(writer, current, current.GetType(), options);
            }
        }

        writer.WriteEndArray();
    }

    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == GenericTypeDefinition;

    protected virtual IEnumerable CreateInstance(Type typeToConvert, Type elementType, IList listInstance)
        => (IEnumerable)TypeUtility.CreateInstance(typeToConvert, args: [listInstance]);

    protected virtual Type GetElementType(Type typeToConvert)
    {
        if (CanConvert(typeToConvert))
        {
            if (IsDictionary)
            {
                return typeof(KeyValuePair<,>).MakeGenericType(typeToConvert.GetGenericArguments());
            }

            return typeToConvert.GetGenericArguments()[0];
        }

        throw new NotSupportedException("The type is not supported.");
    }

    private static IList CreateListInstance(Type elementType)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        return (IList)TypeUtility.CreateInstance(listType, args: [])!;
    }
}
