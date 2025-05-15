using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
// using Libplanet.Serialization.Converters;
using Libplanet.Serialization.Extensions;
using static Libplanet.Serialization.ModelResolver;

namespace Libplanet.Serialization;

public static class ModelSerializer
{
    static ModelSerializer()
    {
        // AddTypeConverter(typeof(BigInteger), typeof(BigIntegerTypeConverter));
        // AddTypeConverter(typeof(bool), typeof(BooleanTypeConverter));
        // AddTypeConverter(typeof(byte[]), typeof(ByteArrayTypeConverter));
        // AddTypeConverter(typeof(byte), typeof(ByteTypeConverter));
        // AddTypeConverter(typeof(DateTimeOffset), typeof(DateTimeOffsetTypeConverter));
        // AddTypeConverter(typeof(Guid), typeof(GuidTypeConverter));
        // AddTypeConverter(typeof(ImmutableArray<byte>), typeof(ImmutableByteArrayTypeConverter));
        // AddTypeConverter(typeof(int), typeof(Int32TypeConverter));
        // AddTypeConverter(typeof(long), typeof(Int64TypeConverter));
        // AddTypeConverter(typeof(string), typeof(StringTypeConverter));
        // AddTypeConverter(typeof(TimeSpan), typeof(TimeSpanTypeConverter));

        static void AddTypeConverter(Type type, Type converterType)
        {
            TypeDescriptor.AddAttributes(type, new TypeConverterAttribute(converterType));
        }
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

    public static void Serialize(Stream stream, object? obj)
    {
        if (obj is null)
        {
            stream.WriteNull();
        }
        else
        {
            Serialize(stream, obj, obj.GetType());
        }
    }

    public static byte[] SerializeToBytes(object? obj)
    {
        using var stream = new MemoryStream();
        Serialize(stream, obj);
        return stream.ToArray();
    }

    public static ImmutableArray<byte> SerializeToImmutableBytes(object? obj)
    {
        using var stream = new MemoryStream();
        Serialize(stream, obj);
        return [.. stream.ToArray()];
    }

    public static object? Deserialize(Stream stream)
    {
        var data = ModelData.GetData(stream);
        var headerType = Type.GetType(data.TypeName)
            ?? throw new ModelSerializationException($"Given type name {data.TypeName} is not found");

        var modelType = ModelResolver.GetType(headerType, data.Version);
        var obj = DeserializeRawValue(stream, modelType)
            ?? throw new ModelSerializationException($"Failed to deserialize {modelType}.");

        return obj;
    }

    public static T Deserialize<T>(Stream stream)
    {
        if (Deserialize(stream) is T obj)
        {
            return obj;
        }

        throw new ModelSerializationException($"Failed to deserialize {typeof(T)}.");
    }

    public static object DeserializeFromBytes(ImmutableArray<byte> bytes)
    {
        using var stream = new MemoryStream([.. bytes]);
        return Deserialize(stream)
            ?? throw new ModelSerializationException(
                $"Failed to deserialize from bytes.");
    }

    public static object DeserializeFromBytes(ReadOnlySpan<byte> bytes)
    {
        using var stream = new MemoryStream(bytes.ToArray());
        return Deserialize(stream)
            ?? throw new ModelSerializationException(
                $"Failed to deserialize from bytes.");
    }

    public static T DeserializeFromBytes<T>(ImmutableArray<byte> bytes)
    {
        using var stream = new MemoryStream([.. bytes]);
        return Deserialize<T>(stream)
            ?? throw new ModelSerializationException(
                $"Failed to deserialize {typeof(T)} from bytes.");
    }

    public static T DeserializeFromBytes<T>(ReadOnlySpan<byte> bytes)
    {
        using var stream = new MemoryStream(bytes.ToArray());
        return Deserialize<T>(stream)
            ?? throw new ModelSerializationException(
                $"Failed to deserialize {typeof(T)} from bytes.");
    }

    public static T Clone<T>(T obj)
    {
        using var stream = new MemoryStream();
        Serialize(stream, obj);
        stream.Position = 0;
        return Deserialize<T>(stream);
    }

    private static void Serialize(Stream stream, object obj, Type type)
    {
        var data = new ModelData
        {
            TypeName = ModelResolver.GetTypeName(type),
            Version = ModelResolver.GetVersion(type),
        };

        data.Write(stream);

        SerializeRawValue(stream, obj, type);
    }

    private static void SerializeRawValue(Stream stream, object? obj, Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            if (obj is null)
            {
                stream.WriteByte(0);
            }
            else
            {
                stream.WriteByte(1);
                SerializeRawValue(stream, obj, nullableType);
            }
        }
        else
        {
            if (obj is null)
            {
                stream.WriteNull();
            }
            else if (type.IsEnum)
            {
                stream.WriteEnum(obj, type);
            }
            else if (TypeDescriptor.GetConverter(type) is TypeConverter converter && converter.CanConvertTo(typeof(byte[])))
            {
                if (converter.ConvertTo(obj, typeof(byte[])) is not byte[] bytes)
                {
                    throw new ModelSerializationException($"Failed to convert {obj} to {type}");
                }

                stream.Write(bytes, 0, bytes.Length);
            }
            else if (TryGetDescriptor(type, out var descriptor))
            {
                var length = 0;
                var items = descriptor.GetValues(obj, type);
                var position = stream.Position;
                stream.WriteInt32(0);

                foreach (var item in items)
                {
                    var actualType = GetActualType(item.Type, item.Value);
                    if (item.Type != actualType)
                    {
                        Serialize(stream, item.Value);
                    }
                    else
                    {
                        SerializeRawValue(stream, item.Value, item.Type);
                    }

                    length++;
                }

                var endPosition = stream.Position;
                stream.Position = position;
                stream.WriteInt32(length);
                stream.Position = endPosition;
            }
            else
            {
                throw new ModelSerializationException($"Unsupported type {obj.GetType()}");
            }
        }
    }

    private static object? DeserializeRawValue(Stream stream, Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            if (stream.ReadByte() == 0)
            {
                return null;
            }
            else if (stream.ReadByte() == 1)
            {
                return DeserializeRawValue(stream, nullableType);
            }
            else
            {
                throw new ModelSerializationException($"Invalid stream for nullable type {type}");
            }
        }
        else
        {
            if (stream.PeekByte() == 0)
            {
                stream.ReadByte();
                return null;
            }
            else if (type.IsEnum)
            {
                return stream.ReadEnum(type);
            }
            else if (TypeDescriptor.GetConverter(type) is TypeConverter converter
                && converter.CanConvertFrom(typeof(byte[])))
            {
                return converter.ConvertFrom(stream);
            }
            else if (TryGetDescriptor(type, out var descriptor))
            {
                var length = stream.ReadInt32();
                var values = descriptor.GetTypes(type, length).Select((itemType, i) =>
                {
                    return ModelData.IsData(stream)
                        ? Deserialize(stream) : DeserializeRawValue(stream, itemType);
                });

                return descriptor.CreateInstance(type, values);
            }
            else
            {
                var message = $"Unsupported type {type}. Cannot convert value of type " +
                              $"{stream.GetType()} to {type}";
                throw new ModelSerializationException(message);
            }

            throw new ModelSerializationException(
                $"Unsupported type {type}. Cannot convert value of type " +
                $"{stream.GetType()} to {type}");
        }
    }

    private static object CreateInstance(Type type, params object?[] args)
    {
        try
        {
            if (Activator.CreateInstance(type, args: args) is { } obj)
            {
                return obj;
            }
        }
        catch (Exception e)
        {
            throw new ModelCreationException(type, e);
        }

        throw new ModelCreationException(type);
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
