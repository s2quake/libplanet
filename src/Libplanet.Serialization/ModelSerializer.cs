using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Bencodex.Types;
using Libplanet.Serialization.Converters;
using static Libplanet.Serialization.ArrayUtility;
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

    public static bool CanSupportType(Type type)
    {
        if (IsNullableType(type))
        {
            return CanSupportType(type.GetGenericArguments()[0]);
        }

        if (IsValueTupleType(type) || IsTupleType(type))
        {
            var genericArguments = type.GetGenericArguments();
            foreach (var genericArgument in genericArguments)
            {
                if (!CanSupportType(genericArgument))
                {
                    return false;
                }
            }

            return true;
        }

        if (IsStandardType(type))
        {
            return true;
        }

        if (type.IsDefined(typeof(ModelAttribute)))
        {
            return true;
        }

        if (IsSupportedArrayType(type, out var elementType))
        {
            return CanSupportType(elementType);
        }

        if (TypeDescriptor.GetConverter(type) is TypeConverter converter
            && converter.CanConvertTo(typeof(IValue))
            && converter.CanConvertFrom(typeof(IValue)))
        {
            return true;
        }

        return false;
    }

    public static IValue Serialize(object? obj) => Serialize(obj, ModelOptions.Default);

    public static IValue Serialize(object? obj, ModelOptions options)
    {
        if (obj is null)
        {
            return Null.Value;
        }

        return Serialize(obj, obj.GetType(), options);
    }

    public static byte[] SerializeToBytes(object? obj) => _codec.Encode(Serialize(obj));

    public static ImmutableArray<byte> SerializeToImmutableBytes(object? obj) => [.. _codec.Encode(Serialize(obj))];

    public static object? Deserialize(IValue value)
        => Deserialize(value, ModelOptions.Default);

    public static object? Deserialize(IValue value, ModelOptions options)
    {
        var data = ModelData.GetData(value);
        var header = data.Header;
        var headerType = Type.GetType(header.TypeName)
            ?? throw new ModelSerializationException($"Given type name {header.TypeName} is not found");

        var modelType = options.GetType(headerType, header.Version);
        var obj = DeserializeRawValue(data.Value, modelType, options)
            ?? throw new ModelSerializationException(
                $"Failed to deserialize {modelType} from {data.Value.Inspect()}.");

        return obj;
    }

    public static T Deserialize<T>(IValue value) => Deserialize<T>(value, ModelOptions.Default);

    public static T Deserialize<T>(IValue value, ModelOptions options)
    {
        if (Deserialize(value, options) is T obj)
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

    private static IValue Serialize(object obj, Type type, ModelOptions options)
    {
        var header = new ModelHeader
        {
            TypeName = options.GetTypeName(type),
            Version = options.GetVersion(type),
        };

        var data = new ModelData
        {
            Header = header,
            Value = SerializeRawValue(obj, type, options),
        };
        return data.Bencoded;
    }

    private static IValue SerializeRawValue(object? obj, Type type, ModelOptions options)
    {
        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            return obj is null ? Null.Value : SerializeRawValue(obj, nullableType, options);
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
        else if (type.IsDefined(typeof(ModelAttribute)) || type.IsDefined(typeof(LegacyModelAttribute)))
        {
            var propertyInfos = options.GetProperties(type);
            var itemList = new List<IValue>(propertyInfos.Length);
            foreach (var propertyInfo in propertyInfos)
            {
                var item = propertyInfo.GetValue(obj);
                var itemType = propertyInfo.PropertyType;
                var actualType = GetPropertyType(propertyInfo, item);
                var serialized = itemType != actualType
                    ? Serialize(item) : SerializeRawValue(item, itemType, options);
                itemList.Add(serialized);
            }

            return new List(itemList);
        }
        else if (IsArray(type, out var elementType)
            || IsImmutableArray(type, out elementType)
            || IsImmutableSortedSet(type, out elementType))
        {
            var items = (IList)obj;
            var list = new List<IValue>(items.Count);

            foreach (var item in items)
            {
                var serialized = item is null ? Null.Value : Serialize(item, elementType, options);
                list.Add(serialized);
            }

            return new List(list);
        }
        else if (IsValueTupleType(type) || IsTupleType(type))
        {
            var genericArguments = type.GetGenericArguments();
            if (obj is not ITuple tuple)
            {
                throw new ModelSerializationException(
                    $"The value {obj} is not a tuple of type {type}");
            }

            if (genericArguments.Length != tuple.Length)
            {
                throw new ModelSerializationException(
                    $"The number of generic arguments {genericArguments.Length} does not match " +
                    $"the number of tuple items {tuple.Length}");
            }

            var list = new List<IValue>(genericArguments.Length);
            for (var i = 0; i < genericArguments.Length; i++)
            {
                var item = tuple[i];
                var itemType = genericArguments[i];
                var serializedValue = SerializeRawValue(item, itemType, options);
                list.Add(serializedValue);
            }

            return new List(list);
        }

        throw new ModelSerializationException($"Unsupported type {obj.GetType()}");
    }

    private static object? DeserializeRawValue(IValue value, Type type, ModelOptions options)
    {
        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            return value is Null ? null : DeserializeRawValue(value, nullableType, options);
        }

        if (IsBencodableType(type))
        {
            return CreateInstance(type, args: [value]);
        }
        else if (IsBencodexType(type))
        {
            if (type.IsAssignableFrom(value.GetType()))
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
        else if (value is Null)
        {
            return null;
        }
        else if (type.IsDefined(typeof(ModelAttribute)))
        {
            var list = (List)value;
            var obj = CreateInstance(type);
            var itemInfos = options.GetProperties(type);
            for (var i = 0; i < itemInfos.Length; i++)
            {
                var itemInfo = itemInfos[i];
                var itemType = itemInfo.PropertyType;
                var serializedValue = list[i];
                var itemValue = ModelData.IsData(serializedValue)
                    ? Deserialize(serializedValue) : DeserializeRawValue(serializedValue, itemType, options);
                itemInfo.SetValue(obj, itemValue);
            }

            return obj;
        }
        else if (type.GetCustomAttribute<LegacyModelAttribute>() is { } legacyModelAttribute)
        {
            var originType = legacyModelAttribute.OriginType;
            var originVersion = options.GetVersion(originType);
            var version = options.GetVersion(type);
            var list = (List)value;
            var obj = CreateInstance(type);
            var itemInfos = options.GetProperties(type);
            for (var i = 0; i < itemInfos.Length; i++)
            {
                var itemInfo = itemInfos[i];
                var itemType = itemInfo.PropertyType;
                var serializedValue = list[i];
                var itemValue = ModelData.IsData(serializedValue)
                    ? Deserialize(serializedValue) : DeserializeRawValue(serializedValue, itemType, options);
                itemInfo.SetValue(obj, itemValue);
            }

            while (version < originVersion)
            {
                var args = new object[] { obj };
                type = options.GetType(originType, version + 1);
                obj = CreateInstance(type, args: args);
                version++;
            }

            return obj;
        }
        else if (IsArray(type, out var elementType))
        {
            if (value is Null)
            {
                return ToEmptyArray(elementType);
            }

            return ToArray((List)value, elementType, options);
        }
        else if (IsImmutableArray(type, out elementType))
        {
            if (value is Null)
            {
                return ToImmutableEmptyArray(elementType);
            }

            return ToImmutableArray((List)value, elementType, options);
        }
        else if (IsImmutableSortedSet(type, out elementType))
        {
            if (value is Null)
            {
                return ToImmutableEmptySortedSet(elementType);
            }

            return ToImmutableSortedSet((List)value, elementType, options);
        }
        else if (IsValueTupleType(type) || IsTupleType(type))
        {
            return ToTupleOrValueTuple((List)value, type, options);
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

    private static Array ToArray(List list, Type elementType, ModelOptions options)
    {
        var array = Array.CreateInstance(elementType, list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i];
            var itemValue = item is Null ? null : Deserialize(item, options);
            array.SetValue(itemValue, i);
        }

        return array;
    }

    private static object ToImmutableArray(
        List list, Type elementType, ModelOptions options)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        var listInstance = (IList)CreateInstance(listType)!;
        foreach (var item in list)
        {
            var itemValue = item is Null ? null : Deserialize(item, options);
            listInstance.Add(itemValue);
        }

        var methodName = nameof(ImmutableArray.CreateRange);
        var methodInfo = GetCreateRangeMethod(
            typeof(ImmutableArray), methodName, typeof(IEnumerable<>));
        var genericMethodInfo = methodInfo.MakeGenericMethod(elementType);
        var methodArgs = new object?[] { listInstance };
        return genericMethodInfo.Invoke(null, parameters: methodArgs)!;
    }

    private static object ToImmutableSortedSet(
        List list, Type elementType, ModelOptions options)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        var listInstance = (IList)CreateInstance(listType)!;
        foreach (var item in list)
        {
            var itemValue = item is Null ? null : Deserialize(item, options);
            listInstance.Add(itemValue);
        }

        var methodName = nameof(ImmutableSortedSet.CreateRange);
        var methodInfo = GetCreateRangeMethod(
            typeof(ImmutableSortedSet), methodName, typeof(IEnumerable<>));
        var genericMethodInfo = methodInfo.MakeGenericMethod(elementType);
        var methodArgs = new object?[] { listInstance };
        return genericMethodInfo.Invoke(null, parameters: methodArgs)!;
    }

    private static object ToTupleOrValueTuple(
        List list, Type tupleType, ModelOptions options)
    {
        var valueList = new List<object?>(list.Count);
        var genericArguments = tupleType.GetGenericArguments();
        for (var i = 0; i < genericArguments.Length; i++)
        {
            var item = list[i];
            var itemType = genericArguments[i];
            var itemValue = DeserializeRawValue(item, itemType, options);
            valueList.Add(itemValue);
        }

        return CreateInstance(tupleType, args: [.. valueList]);
    }

    private static MethodInfo GetCreateRangeMethod(Type type, string methodName, Type parameterType)
    {
        var parameterName = parameterType.Name;
        var bindingFlags = BindingFlags.Public | BindingFlags.Static;
        var methodInfos = type.GetMethods(bindingFlags);

        for (var i = 0; i < methodInfos.Length; i++)
        {
            var methodInfo = methodInfos[i];
            var parameters = methodInfo.GetParameters();
            if (methodInfo.Name == methodName &&
                parameters.Length == 1 &&
                parameters[0].ParameterType.Name == parameterName)
            {
                return methodInfo;
            }
        }

        throw new NotSupportedException("The method is not found.");
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

    private static Type GetPropertyType(PropertyInfo propertyInfo, object? value)
    {
        var type = propertyInfo.PropertyType;
        if (type == typeof(object) || type.IsAbstract || type.IsInterface)
        {
            var propertyAttribute = propertyInfo.GetCustomAttribute<PropertyAttribute>()
                ?? throw new ModelSerializationException(
                    $"The property {propertyInfo.Name} of {propertyInfo.DeclaringType} " +
                    "must be decorated with PropertyAttribute.");
            if (value is not null && propertyAttribute.KnownTypes.Contains(value.GetType()))
            {
                type = value.GetType();
            }
        }

        return type;
    }
}
