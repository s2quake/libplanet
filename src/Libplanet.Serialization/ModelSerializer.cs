using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Libplanet.Serialization.Extensions;
using Libplanet.Serialization.ModelConverters;
using static Libplanet.Serialization.ModelResolver;

namespace Libplanet.Serialization;

public static class ModelSerializer
{
    static ModelSerializer()
    {
        AddModelConverter(typeof(BigInteger), typeof(BigIntegerModelConverter));
        AddModelConverter(typeof(bool), typeof(BooleanModelConverter));
        AddModelConverter(typeof(byte), typeof(ByteModelConverter));
        AddModelConverter(typeof(char), typeof(CharModelConverter));
        AddModelConverter(typeof(DateTimeOffset), typeof(DateTimeOffsetModelConverter));
        AddModelConverter(typeof(Guid), typeof(GuidModelConverter));
        AddModelConverter(typeof(int), typeof(Int32ModelConverter));
        AddModelConverter(typeof(long), typeof(Int64ModelConverter));
        AddModelConverter(typeof(string), typeof(StringModelConverter));
        AddModelConverter(typeof(TimeSpan), typeof(TimeSpanModelConverter));

        static void AddModelConverter(Type type, Type converterType)
        {
            TypeDescriptor.AddAttributes(type, new ModelConverterAttribute(converterType));
        }
    }

    private enum DataType : byte
    {
        Null,

        Enum,

        Converter,

        Descriptor,

        Value,
    }

    public static bool TryGetType(Stream stream, [MaybeNullWhen(false)] out Type type)
    {
        var position = stream.Position;
        try
        {
            if (ModelData.TryGetData(stream, out var data))
            {
                return TypeUtility.TryGetType(data.TypeName, out type);
            }

            type = null;
            return false;
        }
        finally
        {
            stream.Position = position;
        }
    }

    public static void Serialize(Stream stream, object? obj, ModelOptions options)
    {
        if (obj is null)
        {
            stream.WriteByte((byte)DataType.Null);
        }
        else
        {
            Serialize(stream, obj, obj.GetType(), options);
        }
    }

    public static byte[] SerializeToBytes(object? obj)
        => SerializeToBytes(obj, ModelOptions.Empty);

    public static byte[] SerializeToBytes(object? obj, ModelOptions options)
    {
        using var stream = new MemoryStream();
        Serialize(stream, obj, options);
        return stream.ToArray();
    }

    public static object? Deserialize(Stream stream, ModelOptions options)
    {
        var data = ModelData.GetData(stream);
        var headerType = Type.GetType(data.TypeName)
            ?? throw new ModelSerializationException($"Given type name {data.TypeName} is not found");

        var modelType = ModelResolver.GetType(headerType, data.Version);
        var obj = DeserializeRawValue(stream, modelType, options)
            ?? throw new ModelSerializationException($"Failed to deserialize {modelType}.");

        return obj;
    }

    public static T Deserialize<T>(Stream stream, ModelOptions options)
    {
        if (Deserialize(stream, options) is T obj)
        {
            return obj;
        }

        throw new ModelSerializationException($"Failed to deserialize {typeof(T)}.");
    }

    public static object DeserializeFromBytes(ImmutableArray<byte> bytes)
        => DeserializeFromBytes(bytes, ModelOptions.Empty);

    public static object DeserializeFromBytes(ImmutableArray<byte> bytes, ModelOptions options)
    {
        using var stream = new MemoryStream([.. bytes]);
        return Deserialize(stream, options)
            ?? throw new ModelSerializationException(
                $"Failed to deserialize from bytes.");
    }

    public static object DeserializeFromBytes(ReadOnlySpan<byte> bytes)
        => DeserializeFromBytes(bytes, ModelOptions.Empty);

    public static object DeserializeFromBytes(ReadOnlySpan<byte> bytes, ModelOptions options)
    {
        using var stream = new MemoryStream(bytes.ToArray());
        return Deserialize(stream, options)
            ?? throw new ModelSerializationException(
                $"Failed to deserialize from bytes.");
    }

    public static T DeserializeFromBytes<T>(ReadOnlySpan<byte> bytes)
        => DeserializeFromBytes<T>(bytes, ModelOptions.Empty);

    public static T DeserializeFromBytes<T>(ReadOnlySpan<byte> bytes, ModelOptions options)
    {
        using var stream = new MemoryStream(bytes.ToArray());
        return Deserialize<T>(stream, options)
            ?? throw new ModelSerializationException(
                $"Failed to deserialize {typeof(T)} from bytes.");
    }

    public static T Clone<T>(T obj) => Clone(obj, ModelOptions.Empty);

    public static T Clone<T>(T obj, ModelOptions options)
    {
        using var stream = new MemoryStream();
        Serialize(stream, obj, options);
        stream.Position = 0;
        return Deserialize<T>(stream, options);
    }

    private static void Serialize(Stream stream, object obj, Type type, ModelOptions options)
    {
        var data = new ModelData
        {
            TypeName = GetTypeName(type),
            Version = GetVersion(type),
        };

        data.Write(stream);

        SerializeRawValue(stream, obj, type, options);
    }

    private static void SerializeRawValue(Stream stream, object? obj, Type type, ModelOptions options)
    {
        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            if (obj is null)
            {
                stream.WriteByte((byte)DataType.Null);
            }
            else
            {
                stream.WriteByte((byte)DataType.Value);
                SerializeRawValue(stream, obj, nullableType, options);
            }
        }
        else
        {
            if (obj is null)
            {
                stream.WriteByte((byte)DataType.Null);
            }
            else if (type.IsEnum)
            {
                stream.WriteByte((byte)DataType.Enum);
                stream.WriteEnum(obj, type);
            }
            else if (TryGetConverter(type, out var converter))
            {
                stream.WriteByte((byte)DataType.Converter);
                converter.Serialize(obj, stream, options);
#if _POSITION
                System.Diagnostics.Trace.WriteLine($"<< {type} {stream.Position}");
#endif
            }
            else if (TryGetDescriptor(type, out var descriptor))
            {
                var itemTypes = descriptor.GetTypes(type, out var isArray);
                var values = descriptor.Serialize(obj, type, options);
                var length = values.Length;
                stream.WriteByte((byte)DataType.Descriptor);
                stream.WriteInt32(length);

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
                    var actualType = GetActualType(itemType, value);
                    if (itemType != actualType)
                    {
                        Serialize(stream, value, options);
                    }
                    else
                    {
                        SerializeRawValue(stream, value, itemType, options);
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

    private static object? DeserializeRawValue(Stream stream, Type type, ModelOptions options)
    {
        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            var dataType = (DataType)stream.ReadByte();
            if (dataType == DataType.Null)
            {
                return null;
            }
            else if (dataType == DataType.Value)
            {
                return DeserializeRawValue(stream, nullableType, options);
            }
            else
            {
                throw new ModelSerializationException($"Invalid stream for nullable type {type}");
            }
        }
        else
        {
            var dataType = (DataType)stream.ReadByte();
            if (dataType == DataType.Null)
            {
                return null;
            }
            else if (type.IsEnum)
            {
                if (dataType != DataType.Enum)
                {
                    throw new ModelSerializationException(
                        $"Invalid stream for enum type {type}");
                }

                return stream.ReadEnum(type);
            }
            else if (TryGetConverter(type, out var converter))
            {
                if (dataType != DataType.Converter)
                {
                    throw new ModelSerializationException(
                        $"Invalid stream for converter type {type}");
                }

                var value = converter.Deserialize(stream, options);
#if _POSITION
                System.Diagnostics.Trace.WriteLine($">> {type} {stream.Position}");
#endif
                return value;
            }
            else if (TryGetDescriptor(type, out var descriptor))
            {
                if (dataType != DataType.Descriptor)
                {
                    throw new ModelSerializationException(
                        $"Invalid stream for descriptor type {type}");
                }

                var length = stream.ReadInt32();
                var itemTypes = descriptor.GetTypes(type, out var isArray);
                if (isArray && itemTypes.Length != 1)
                {
                    throw new ModelSerializationException(
                        $"The number of types ({itemTypes.Length}) does not match the number of items " +
                        $"({length})");
                }

                if (!isArray && length != itemTypes.Length)
                {
                    throw new ModelSerializationException(
                        $"The number of items ({length}) does not match the number of types " +
                        $"({itemTypes.Length})");
                }

                var values = new object?[length];
                for (var i = 0; i < length; i++)
                {
                    var itemType = isArray ? itemTypes[0] : itemTypes[i];
                    values[i] = ModelData.IsData(stream)
                        ? Deserialize(stream, options) : DeserializeRawValue(stream, itemType, options);
                }

#if _POSITION
                System.Diagnostics.Trace.WriteLine($">> {type} {stream.Position}");
#endif
                return descriptor.Deserialize(type, values, options);
            }
            else
            {
                var message = $"Unsupported type {type}. Cannot convert value of type " +
                              $"{stream.GetType()} to {type}";
                throw new ModelSerializationException(message);
            }
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
