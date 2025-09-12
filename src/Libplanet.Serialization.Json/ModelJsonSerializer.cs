using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Libplanet.Serialization.Json.ModelJsonResolver;

namespace Libplanet.Serialization.Json;

public static class ModelJsonSerializer
{
    public static string SerializeToString(object? obj) => SerializeToString(obj, ModelOptions.Empty);

    public static string SerializeToString(object? obj, ModelOptions options)
    {
        using var stream = new MemoryStream();

        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        Serialize(writer, obj, options);
        writer.WriteEndObject();
        writer.Flush();
        stream.Position = 0;
        using var streamReader = new StreamReader(stream);

        return streamReader.ReadToEnd();
    }

    public static void Serialize(Utf8JsonWriter writer, object? obj, ModelOptions options)
    {
        if (obj is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            Serialize(writer, obj, obj.GetType(), options);
        }

    }

    public static T Deserialize<T>(ref Utf8JsonReader reader, ModelOptions options)
        where T : notnull
    {
        if (Deserialize(ref reader, options) is T obj)
        {
            return obj;
        }

        throw new ModelSerializationException($"Failed to deserialize {typeof(T)}.");
    }

    public static object DeserializeFromString(string json)
        => DeserializeFromString(json, ModelOptions.Empty);

    public static object DeserializeFromString(string json, ModelOptions options)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes.AsSpan());
        return Deserialize(ref reader, options)
            ?? throw new ModelSerializationException(
                $"Failed to deserialize from bytes.");
    }

    public static T DeserializeFromString<T>(string json)
        where T : notnull
        => DeserializeFromString<T>(json, ModelOptions.Empty);

    public static T DeserializeFromString<T>(string json, ModelOptions options)
        where T : notnull
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes.AsSpan());
        reader.Read();
        return Deserialize<T>(ref reader, options)
            ?? throw new ModelSerializationException(
                $"Failed to deserialize {typeof(T)} from bytes.");
    }

    public static object? Deserialize(ref Utf8JsonReader reader, ModelOptions options)
    {

        // var data = ModelData.GetData(reader);
        // var headerType = TypeUtility.GetType(data.TypeName)
        //     ?? throw new ModelSerializationException($"Given type name {data.TypeName} is not found");

        // var modelType = ModelResolver.GetType(headerType, data.Version);
        // var obj = DeserializeRawValue(reader, modelType, options)
        //     ?? throw new ModelSerializationException($"Failed to deserialize {modelType}.");

        // return obj;

        return null;
    }

    private static void Serialize(Utf8JsonWriter writer, object obj, Type type, ModelOptions options)
    {
        writer.WriteString("type", GetTypeName(type));
        writer.WriteNumber("version", GetVersion(type));
        writer.WriteStartObject("value");
        SerializeRawValue(writer, obj, type, options);
        writer.WriteEndObject();
    }

    private static void SerializeRawValue(Utf8JsonWriter writer, object? obj, Type type, ModelOptions options)
    {
        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            if (obj is null)
            {
                writer.WriteNullValue();
                // stream.WriteByte((byte)DataType.Null);
            }
            else if (TypeUtility.IsDefault(obj, nullableType))
            {
                // stream.WriteByte((byte)DataType.Default);
            }
            else
            {
                // stream.WriteByte((byte)DataType.Value);
                SerializeRawValue(writer, obj, nullableType, options);
            }
        }
        else
        {
            if (obj is null)
            {
                writer.WriteNullValue();
                // stream.WriteByte((byte)DataType.Null);
            }
            else if (TypeUtility.IsDefault(obj, type))
            {
                // stream.WriteByte((byte)DataType.Default);
            }
            else if (type.IsEnum)
            {
                writer.WriteStringValue(obj.ToString());
                // stream.WriteByte((byte)DataType.Enum);
                // stream.WriteEnum(obj, type);
            }
            else if (TryGetConverter(type, out var converter))
            {
                // stream.WriteByte((byte)DataType.Converter);
                SerializeByConverter(writer, obj, options, converter);
#if _POSITION
                System.Diagnostics.Trace.WriteLine($"<< {type} {stream.Position}");
#endif
            }
            else if (TryGetDescriptor(type, out var descriptor))
            {
                var itemTypes = descriptor.GetTypes(type, out var isArray);
                var values = descriptor.Serialize(obj, type, options);
                var length = values.Length;
                // stream.WriteByte((byte)DataType.Descriptor);
                // stream.WriteInt32(length);

                if (isArray && itemTypes.Length != 1)
                {
                    throw new ModelSerializationException(
                        $"The number of types ({itemTypes.Length}) does not match the number of items " +
                        $"({values.Length})");
                }

                for (var i = 0; i < values.Length; i++)
                {
                    var itemType = isArray ? itemTypes[0] : itemTypes[i];
                    var value = values[i];
                    var actualType = GetActualType(itemType.Item2, value);
                    if (itemType.Item2 != actualType)
                    {
                        Serialize(writer, value, options);
                    }
                    else
                    {
                        writer.WritePropertyName(JsonNamingPolicy.CamelCase.ConvertName(itemType.Item1));
                        SerializeRawValue(writer, value, itemType.Item2, options);
                    }
                }

#if _POSITION
                System.Diagnostics.Trace.WriteLine($"<< {type} {stream.Position}");
#endif
            }
            else
            {
                throw new ModelSerializationException($"Unsupported type {obj.GetType()}");
            }
        }
    }

    private static void SerializeByConverter(
            Utf8JsonWriter writer, object obj, ModelOptions options, JsonConverter converter)
    {
        try
        {
            // BigIntegerJsonConverter d;
            // d.Write
            // converter.Write(writer, obj, new JsonSerializerOptions());
            // converter.Serialize(obj, stream, options);

            var methodInfo = converter.GetType().GetMethod("Write");
            methodInfo.Invoke(converter, [writer, obj, new JsonSerializerOptions()]);
        }
        catch (Exception e)
        {
            var message = $"An exception occurred while serializing {obj.GetType()} by {converter.GetType()}.";
            throw new ModelSerializationException(message, e);
        }
    }

    private static Type GetActualType(Type type, object? value)
    {
        if (value is not null && (type == typeof(object) || type.IsAbstract || type.IsInterface))
        {
            return value.GetType();
        }

        return type;
    }
}
