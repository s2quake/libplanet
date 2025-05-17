using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Libplanet.Serialization.Extensions;
using Libplanet.Serialization.ModelConverters;
using static Libplanet.Serialization.ModelResolver;

namespace Libplanet.Serialization;

public static class ModelSerializer
{
    private enum DataType : byte
    {
        Null,

        Enum,

        Converter,

        Descriptor,

        Value,
    }

    static ModelSerializer()
    {
        AddModelConverter(typeof(BigInteger), typeof(BigIntegerModelConverter));
        AddModelConverter(typeof(bool), typeof(BooleanModelConverter));
        AddModelConverter(typeof(byte), typeof(ByteModelConverter));
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
            TypeName = GetTypeName(type),
            Version = GetVersion(type),
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
                stream.WriteByte((byte)DataType.Null);
            }
            else
            {
                stream.WriteByte((byte)DataType.Value);
                SerializeRawValue(stream, obj, nullableType);
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
                converter.Serialize(obj, stream);
                System.Diagnostics.Trace.WriteLine($"<< {type} {stream.Position}");
            }
            else if (TryGetDescriptor(type, out var descriptor))
            {
                var itemTypes = descriptor.GetTypes(type, out var isArray);
                var values = descriptor.GetValues(obj, type);
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
                        Serialize(stream, value);
                    }
                    else
                    {
                        SerializeRawValue(stream, value, itemType);
                    }
                }

                System.Diagnostics.Trace.WriteLine($"<< {type} {stream.Position}");
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
            var dataType = (DataType)stream.ReadByte();
            if (dataType == DataType.Null)
            {
                return null;
            }
            else if (dataType == DataType.Value)
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

                var value = converter.Deserialize(stream);
                System.Diagnostics.Trace.WriteLine($">> {type} {stream.Position}");
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
                        ? Deserialize(stream) : DeserializeRawValue(stream, itemType);
                }

                System.Diagnostics.Trace.WriteLine($">> {type} {stream.Position}");

                return descriptor.CreateInstance(type, values);
            }
            else
            {
                var message = $"Unsupported type {type}. Cannot convert value of type " +
                              $"{stream.GetType()} to {type}";
                throw new ModelSerializationException(message);
            }
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
