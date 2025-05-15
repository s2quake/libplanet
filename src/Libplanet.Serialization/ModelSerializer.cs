using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Bencodex.Types;
using Libplanet.Serialization.Converters;
using static Libplanet.Serialization.ModelResolver;
using static Libplanet.Serialization.TypeUtility;

namespace Libplanet.Serialization;

public static class ModelSerializer
{
    private static readonly Codec _codec = new();

    static ModelSerializer()
    {
        AddTypeConverter(typeof(BigInteger), typeof(BigIntegerTypeConverter));
        AddTypeConverter(typeof(bool), typeof(BooleanTypeConverter));
        AddTypeConverter(typeof(byte[]), typeof(ByteArrayTypeConverter));
        AddTypeConverter(typeof(byte), typeof(ByteTypeConverter));
        AddTypeConverter(typeof(DateTimeOffset), typeof(DateTimeOffsetTypeConverter));
        AddTypeConverter(typeof(Guid), typeof(GuidTypeConverter));
        AddTypeConverter(typeof(ImmutableArray<byte>), typeof(ImmutableByteArrayTypeConverter));
        AddTypeConverter(typeof(int), typeof(Int32TypeConverter));
        AddTypeConverter(typeof(long), typeof(Int64TypeConverter));
        AddTypeConverter(typeof(string), typeof(StringTypeConverter));
        AddTypeConverter(typeof(TimeSpan), typeof(TimeSpanTypeConverter));

        static void AddTypeConverter(Type type, Type converterType)
        {
            TypeDescriptor.AddAttributes(type, new TypeConverterAttribute(converterType));
        }
    }

    public static bool TryGetType(IValue value, [MaybeNullWhen(false)] out Type type)
    {
        if (ModelData.TryGetData(value, out var data))
        {
            var header = data.Header;
            return TypeUtility.TryGetType(header.TypeName, out type);
        }

        type = null;
        return false;
    }

    public static IValue Serialize(object? obj)
    {
        if (obj is null)
        {
            return Null.Value;
        }

        return Serialize(obj, obj.GetType());
    }

    public static byte[] SerializeToBytes(object? obj) => _codec.Encode(Serialize(obj));

    public static ImmutableArray<byte> SerializeToImmutableBytes(object? obj) => [.. _codec.Encode(Serialize(obj))];

    public static object? Deserialize(IValue value)
    {
        var data = ModelData.GetData(value);
        var header = data.Header;
        var headerType = Type.GetType(header.TypeName)
            ?? throw new ModelSerializationException($"Given type name {header.TypeName} is not found");

        var modelType = ModelResolver.GetType(headerType, header.Version);
        var obj = DeserializeRawValue(data.Value, modelType)
            ?? throw new ModelSerializationException(
                $"Failed to deserialize {modelType} from {data.Value.Inspect()}.");

        return obj;
    }

    public static T Deserialize<T>(IValue value)
    {
        if (Deserialize(value) is T obj)
        {
            return obj;
        }

        throw new ModelSerializationException(
            $"Failed to deserialize {typeof(T)} from {value.Inspect()}.");
    }

    public static T DeserializeFromBytes<T>(ImmutableArray<byte> bytes)
    {
        return Deserialize<T>(_codec.Decode([.. bytes]))
            ?? throw new ModelSerializationException(
                $"Failed to deserialize {typeof(T)} from bytes.");
    }

    public static T DeserializeFromBytes<T>(ReadOnlySpan<byte> bytes)
    {
        return Deserialize<T>(_codec.Decode([.. bytes]))
            ?? throw new ModelSerializationException(
                $"Failed to deserialize {typeof(T)} from bytes.");
    }

    public static T Clone<T>(T obj) => Deserialize<T>(Serialize(obj));

    private static IValue Serialize(object obj, Type type)
    {
        var header = new ModelHeader
        {
            TypeName = ModelResolver.GetTypeName(type),
            Version = ModelResolver.GetVersion(type),
        };

        var data = new ModelData
        {
            Header = header,
            Value = SerializeRawValue(obj, type),
        };
        return data.Bencoded;
    }

    private static IValue SerializeRawValue(object? obj, Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            return obj is null ? Null.Value : SerializeRawValue(obj, nullableType);
        }

        if (obj is null)
        {
            return Null.Value;
        }
        else if (obj is IBencodable bencodable)
        {
            return bencodable.Bencoded;
        }
        else if (obj is IValue bencoded)
        {
            return bencoded;
        }
        else if (type.IsEnum)
        {
            var underlyingType = Enum.GetUnderlyingType(type);
            if (underlyingType == typeof(int))
            {
                return new Integer((int)obj);
            }
            else if (underlyingType == typeof(long))
            {
                return new Integer((long)obj);
            }
        }
        else if (TypeDescriptor.GetConverter(type) is TypeConverter converter && converter.CanConvertTo(typeof(IValue)))
        {
            return converter.ConvertTo(obj, typeof(IValue)) is IValue v
                ? v : throw new ModelSerializationException($"Failed to convert {obj} to {type}");
        }
        else if (TryGetDescriptor(type, out var descriptor))
        {
            var items = descriptor.GetValues(obj, type).Select(item =>
            {
                var actualType = GetActualType(item.Type, item.Value);
                var serialized = item.Type != actualType
                    ? Serialize(item.Value) : SerializeRawValue(item.Value, item.Type);
                return serialized;
            });

            return new List(items);
        }

        throw new ModelSerializationException($"Unsupported type {obj.GetType()}");
    }

    private static object? DeserializeRawValue(IValue value, Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            return value is Null ? null : DeserializeRawValue(value, nullableType);
        }

        if (value is Null)
        {
            return GetDefault(type);
        }
        else if (IsBencodableType(type))
        {
            return CreateInstance(type, args: [value]);
        }
        else if (IsBencodexType(type))
        {
            if (type.IsInstanceOfType(value))
            {
                return value;
            }
        }
        else if (type.IsEnum)
        {
            if (value is Integer integer)
            {
                var underlyingType = Enum.GetUnderlyingType(type);
                if (underlyingType == typeof(long))
                {
                    return Enum.ToObject(type, (long)integer.Value);
                }
                else if (underlyingType == typeof(int))
                {
                    return Enum.ToObject(type, (int)integer.Value);
                }
            }
        }
        else if (TypeDescriptor.GetConverter(type) is TypeConverter converter
            && converter.CanConvertFrom(typeof(IValue)))
        {
            return converter.ConvertFrom(value);
        }
        else if (TryGetDescriptor(type, out var descriptor))
        {
            var list = (List)value;
            var values = descriptor.GetTypes(type, list.Count).Select((itemType, i) =>
            {
                var serializedValue = list[i];
                return ModelData.IsData(serializedValue)
                    ? Deserialize(serializedValue) : DeserializeRawValue(serializedValue, itemType);
            });

            return descriptor.CreateInstance(type, values);
        }
        else
        {
            var message = $"Unsupported type {type}. Cannot convert value of type " +
                          $"{value.GetType()} to {type}";
            throw new ModelSerializationException(message);
        }

        throw new ModelSerializationException(
            $"Unsupported type {type}. Cannot convert value of type " +
            $"{value.GetType()} to {type}");
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
