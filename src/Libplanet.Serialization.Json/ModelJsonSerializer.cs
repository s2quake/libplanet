// using System.IO;
// using System.Text.Json;
// using static Libplanet.Serialization.ModelResolver;

// namespace Libplanet.Serialization.Json;

// public static class ModelJsonSerializer
// {
//     public static string Serialize(object obj)
//     {
//         using var stream = new MemoryStream();
//         using var writer = new Utf8JsonWriter(stream);

//         writer.WriteString("type", "you");

//     }

//     public static void Serialize(Stream stream, object? obj, ModelOptions options)
//     {
//         using var writer = new Utf8JsonWriter(stream);
//         if (obj is null)
//         {
//             writer.WriteNullValue();
//         }
//         else
//         {
//             Serialize(stream, obj, obj.GetType(), options);
//         }
//     }

//     private static void Serialize(Utf8JsonWriter writer, object obj, Type type, ModelOptions options)
//     {
//         writer.WriteString("type", GetTypeName(type));
//         writer.WriteNumber("version", GetVersion(type));
//         // var data = new ModelData
//         // {
//         //     TypeName = GetTypeName(type),
//         //     Version = GetVersion(type),
//         // };

//         // data.Write(stream);

//         SerializeRawValue(stream, obj, type, options);
//     }

//     private static void SerializeRawValue(Utf8JsonWriter writer, object? obj, Type type, ModelOptions options)
//     {
//         if (Nullable.GetUnderlyingType(type) is { } nullableType)
//         {
//             if (obj is null)
//             {
//                 writer.WriteNullValue();
//                 // stream.WriteByte((byte)DataType.Null);
//             }
//             else if (TypeUtility.IsDefault(obj, nullableType))
//             {
//                 // stream.WriteByte((byte)DataType.Default);
//             }
//             else
//             {
//                 // stream.WriteByte((byte)DataType.Value);
//                 SerializeRawValue(writer, obj, nullableType, options);
//             }
//         }
//         else
//         {
//             if (obj is null)
//             {
//                 writer.WriteNullValue();
//                 // stream.WriteByte((byte)DataType.Null);
//             }
//             else if (TypeUtility.IsDefault(obj, type))
//             {
//                 // stream.WriteByte((byte)DataType.Default);
//             }
//             else if (type.IsEnum)
//             {
//                 writer.WriteStringValue(obj.ToString());
//                 // stream.WriteByte((byte)DataType.Enum);
//                 // stream.WriteEnum(obj, type);
//             }
//             else if (TryGetConverter(type, out var converter))
//             {
//                 // stream.WriteByte((byte)DataType.Converter);
//                 // SerializeByConverter(stream, obj, options, converter);
// #if _POSITION
//                 System.Diagnostics.Trace.WriteLine($"<< {type} {stream.Position}");
// #endif
//             }
//             else if (TryGetDescriptor(type, out var descriptor))
//             {
//                 // var itemTypes = descriptor.GetTypes(type, out var isArray);
//                 // var values = descriptor.Serialize(obj, type, options);
//                 // var length = values.Length;
//                 // stream.WriteByte((byte)DataType.Descriptor);
//                 // stream.WriteInt32(length);

//                 // if (isArray && itemTypes.Length != 1)
//                 // {
//                 //     throw new ModelSerializationException(
//                 //         $"The number of types ({itemTypes.Length}) does not match the number of items " +
//                 //         $"({values.Length})");
//                 // }

//                 // for (var i = 0; i < values.Length; i++)
//                 // {
//                 //     var itemType = isArray ? itemTypes[0] : itemTypes[i];
//                 //     var value = values[i];
//                 //     var actualType = GetActualType(itemType, value);
//                 //     if (itemType != actualType)
//                 //     {
//                 //         Serialize(stream, value, options);
//                 //     }
//                 //     else
//                 //     {
//                 //         SerializeRawValue(stream, value, itemType, options);
//                 //     }
//                 // }

// #if _POSITION
//                 System.Diagnostics.Trace.WriteLine($"<< {type} {stream.Position}");
// #endif
//             }
//             else
//             {
//                 throw new ModelSerializationException($"Unsupported type {obj.GetType()}");
//             }
//         }
//     }
// }
