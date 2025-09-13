using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Libplanet.Serialization.Json.Converters;
using static Libplanet.Serialization.Json.ModelJsonResolver;

namespace Libplanet.Serialization.Json;

public static class ModelJsonSerializer
{
    public static string SerializeToString(object? obj) => SerializeToString(obj, ModelOptions.Empty);

    public static string SerializeToString(object? obj, ModelOptions options)
    {
        // using var stream = new MemoryStream();

        // using var writer = new Utf8JsonWriter(stream);
        // writer.WriteStartObject();
        // Serialize(writer, obj, options);
        // writer.WriteEndObject();
        // writer.Flush();
        // stream.Position = 0;
        // using var streamReader = new StreamReader(stream);

        // return streamReader.ReadToEnd();

        return JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new ModelJsonConverter(),
            },
        });

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
        reader.ReadStartObject();
        return Deserialize<T>(ref reader, options)
            ?? throw new ModelSerializationException(
                $"Failed to deserialize {typeof(T)} from bytes.");
    }

    public static object? Deserialize(ref Utf8JsonReader reader, ModelOptions options)
    {
        var typeName = reader.ReadString("type");
        var version = reader.ReadInt32("version");

        var type = TypeUtility.GetType(typeName);
        var modelType = ModelResolver.GetType(type, version);

        reader.ReadPropertyName("value");
        var obj = DeserializeRawValue(ref reader, modelType, options)
            ?? throw new ModelSerializationException($"Failed to deserialize {modelType}.");

        return obj;
    }

    private static void Serialize(Utf8JsonWriter writer, object obj, Type type, ModelOptions options)
    {
        writer.WriteString("type", GetTypeName(type));
        writer.WriteNumber("version", GetVersion(type));
        writer.WritePropertyName("value");
        SerializeRawValue(writer, obj, type, options);
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
//                 var itemTypes = descriptor.GetTypes(type, out var isArray);
//                 var values = descriptor.Serialize(obj, type, options);

//                 if (isArray && itemTypes.Length != 1)
//                 {
//                     throw new ModelSerializationException(
//                         $"The number of types ({itemTypes.Length}) does not match the number of items " +
//                         $"({values.Length})");
//                 }

//                 if (isArray)
//                 {
//                     writer.WriteStartArray();
//                 }
//                 else
//                 {
//                     writer.WriteStartObject();
//                 }

//                 for (var i = 0; i < values.Length; i++)
//                 {
//                     var itemType = isArray ? itemTypes[0] : itemTypes[i];
//                     var value = values[i];
//                     var actualType = GetActualType(itemType.Item2, value);
//                     if (itemType.Item2 != actualType)
//                     {
//                         Serialize(writer, value, options);
//                     }
//                     else if (isArray)
//                     {
//                         SerializeRawValue(writer, value, itemType.Item2, options);
//                     }
//                     else
//                     {
//                         writer.WritePropertyName(JsonNamingPolicy.CamelCase.ConvertName(itemType.Item1));
//                         SerializeRawValue(writer, value, itemType.Item2, options);
//                     }
//                 }

//                 if (isArray)
//                 {
//                     writer.WriteEndArray();
//                 }
//                 else
//                 {
//                     writer.WriteEndObject();
//                 }

// #if _POSITION
//                 System.Diagnostics.Trace.WriteLine($"<< {type} {stream.Position}");
// #endif
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

    private static object? DeserializeRawValue(ref Utf8JsonReader reader, Type type, ModelOptions options)
    {
        throw new NotImplementedException();
        if (!reader.Read())
        {
            throw new JsonException("Unexpected end of JSON.");
        }

        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            // var dataType = (DataType)stream.ReadByte();
            // if (dataType == DataType.Null)
            // {
            //     return null;
            // }
            // else if (dataType == DataType.Default)
            // {
            //     return TypeUtility.GetDefault(nullableType);
            // }
            // else if (dataType == DataType.Value)
            // {
            //     return DeserializeRawValue(stream, nullableType, options);
            // }
            // else
            // {
            //     throw new ModelSerializationException($"Invalid stream for nullable type {type}");
            // }
            throw new NotImplementedException();
        }
        else
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            else if (reader.TokenType is JsonTokenType.EndArray or JsonTokenType.EndObject)
            {
                return TypeUtility.GetDefault(type);
            }
            else if (type.IsEnum)
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException($"Property must be a string for enum type {type}.");
                }

                var enumString = reader.GetString()!;
                if (Enum.TryParse(type, enumString, out var enumValue))
                {
                    return enumValue;
                }
                else
                {
                    throw new JsonException($"Invalid value '{enumString}' for enum type {type}.");
                }
            }
            else if (reader.TokenType is JsonTokenType.Number
                or JsonTokenType.String
                or JsonTokenType.True
                or JsonTokenType.False)
            {
                if (!TryGetConverter(type, out var converter))
                {
                    throw new JsonException($"No converter found for type {type}.");
                }

                return JsonSerializer.Deserialize(ref reader, type, new JsonSerializerOptions
                {
                    Converters = { converter }
                });
            }
            else if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
            {
                // if (!TryGetDescriptor(type, out var descriptor))
                // {
                //     throw new JsonException($"No converter found for type {type}.");
                // }

                // var itemTypes = descriptor.GetTypes(type, out var isArray);
                // // var isArray = reader.TokenType == JsonTokenType.StartArray;
                // if (isArray)
                // {
                //     var itemType = itemTypes[0].Item2;
                //     var valueList = new List<object?>();

                //     reader.ReadStartArray();

                //     while (reader.TokenType != JsonTokenType.EndArray)
                //     {
                //         // array의 각 아이템을 읽음
                //         reader.Read();
                //         var item = DeserializeRawValue(ref reader, itemType, options);
                //         valueList.Add(item);
                //     }

                //     reader.ReadEndArray();

                //     return descriptor.Deserialize(type, [.. valueList], options);

                // }
                // else
                // {
                //     var values = new object?[itemTypes.Length];
                //     var found = new bool[itemTypes.Length];

                //     reader.ReadStartObject();

                //     while (reader.TokenType != JsonTokenType.EndObject)
                //     {
                //         var propertyName = reader.ReadPropertyName();
                //         var index = Array.FindIndex(
                //         itemTypes,
                //         it => string.Equals(
                //                 JsonNamingPolicy.CamelCase.ConvertName(it.Item1),
                //                 propertyName,
                //                 StringComparison.Ordinal));
                //         if (index < 0)
                //         {
                //             throw new JsonException($"Unknown property '{propertyName}' for {type}.");
                //         }

                //         if (found[index])
                //         {
                //             throw new JsonException($"Duplicate property '{propertyName}' for {type}.");
                //         }

                //         var itemType = itemTypes[index].Item2;
                //         var item = DeserializeRawValue(ref reader, itemType, options);
                //         values[index] = item;
                //         found[index] = true;
                //     }

                //     reader.ReadEndObject();

                //     for (var i = 0; i < found.Length; i++)
                //     {
                //         if (!found[i])
                //         {
                //             throw new JsonException($"Missing property '{itemTypes[i].Item1}' for {type}.");
                //         }
                //     }

                //     return descriptor.Deserialize(type, values, options);
                // }

                //                 if (!isArray && length != itemTypes.Length)
                //                 {
                //                     throw new ModelSerializationException(
                //                         $"The number of items ({length}) does not match the number of types " +
                //                         $"({itemTypes.Length})");
                //                 }

                // var values = new object?[length];
                // for (var i = 0; i < length; i++)
                // {
                //     var itemType = isArray ? itemTypes[0] : itemTypes[i];
                //     values[i] = ModelData.IsData(stream)
                //         ? Deserialize(stream, options) : DeserializeRawValue(stream, itemType, options);
                // }

                // #if _POSITION
                //                 System.Diagnostics.Trace.WriteLine($">> {type} {stream.Position}");
                // #endif
                //                 return descriptor.Deserialize(type, values, options);
            }

            // else if (reader.TokenType is JsonTokenType.Number
            //     or JsonTokenType.String
            //     or JsonTokenType.True
            //     or JsonTokenType.False)
            // {
            //     // Primitive value
            //     return DeserializeRawValue(ref reader, type, options);
            // }
            // else if (reader.TokenType is JsonTokenType.StartObject)
            // {
            //     throw new NotImplementedException();
            // }
            // else if (reader.TokenType is JsonTokenType.StartArray)
            // {
            //     throw new NotImplementedException();
            // }
            else
            {
                throw new JsonException($"Unexpected token {reader.TokenType} when deserializing {type}.");
            }
            //             var dataType = (DataType)stream.ReadByte();
            //             if (dataType == DataType.Null)
            //             {
            //                 return null;
            //             }
            //             else if (dataType == DataType.Default)
            //             {
            //                 return TypeUtility.GetDefault(type);
            //             }
            //             else if (dataType == DataType.Value)
            //             {
            //                 return DeserializeRawValue(stream, type, options);
            //             }
            //             else if (type.IsEnum)
            //             {
            //                 if (dataType != DataType.Enum)
            //                 {
            //                     throw new ModelSerializationException(
            //                         $"Invalid stream for enum type {type}");
            //                 }

            //                 return stream.ReadEnum(type);
            //             }
            //             else if (TryGetConverter(type, out var converter))
            //             {
            //                 if (dataType != DataType.Converter)
            //                 {
            //                     throw new ModelSerializationException(
            //                         $"Invalid stream for converter type {type}");
            //                 }

            //                 var value = converter.Deserialize(stream, options);
            // #if _POSITION
            //                 System.Diagnostics.Trace.WriteLine($">> {type} {stream.Position}");
            // #endif
            //                 return value;
            //             }
            //             else if (TryGetDescriptor(type, out var descriptor))
            //             {
            //                 if (dataType != DataType.Descriptor)
            //                 {
            //                     throw new ModelSerializationException(
            //                         $"Invalid stream for descriptor type {type}");
            //                 }

            //                 var length = stream.ReadInt32();
            //                 var itemTypes = descriptor.GetTypes(type, out var isArray);
            //                 if (isArray && itemTypes.Length != 1)
            //                 {
            //                     throw new ModelSerializationException(
            //                         $"The number of types ({itemTypes.Length}) does not match the number of items " +
            //                         $"({length})");
            //                 }

            //                 if (!isArray && length != itemTypes.Length)
            //                 {
            //                     throw new ModelSerializationException(
            //                         $"The number of items ({length}) does not match the number of types " +
            //                         $"({itemTypes.Length})");
            //                 }

            //                 var values = new object?[length];
            //                 for (var i = 0; i < length; i++)
            //                 {
            //                     var itemType = isArray ? itemTypes[0] : itemTypes[i];
            //                     values[i] = ModelData.IsData(stream)
            //                         ? Deserialize(stream, options) : DeserializeRawValue(stream, itemType, options);
            //                 }

            // #if _POSITION
            //                 System.Diagnostics.Trace.WriteLine($">> {type} {stream.Position}");
            // #endif
            //                 return descriptor.Deserialize(type, values, options);
            //             }
            //             else
            //             {
            //                 var message = $"Unsupported type {type}. Cannot convert value of type " +
            //                               $"{stream.GetType()} to {type}";
            //                 throw new ModelSerializationException(message);
            //             }
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
