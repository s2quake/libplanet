using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Bencodex.Types;
using Libplanet.Serialization.Converters;
using static Libplanet.Serialization.ArrayUtility;
using static Libplanet.Serialization.TypeUtility;

namespace Libplanet.Serialization;

public static class ModelSerializer
{
    private const int ObjectValue = 0x_0000_0001;
    private const int ArrayValue = 0x_0000_0002;
    private const int ImmutableArrayValue = 0x_0000_0003;
    private const int ImmutableSortedSetValue = 0x_0000_0004;
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
        if (ModelData.TryGetObject(value, out var data))
        {
            var header = data.Header;
            if (header.TypeValue == ObjectValue)
            {
                return TypeUtility.TryGetType(header.TypeName, out type);
            }
            else if (header.TypeValue == ArrayValue)
            {
                var elementType = TypeUtility.GetType(header.TypeName);
                type = GetArrayType(elementType);
                return true;
            }
            else if (header.TypeValue == ImmutableArrayValue)
            {
                var elementType = TypeUtility.GetType(header.TypeName);
                type = GetImmutableArrayType(elementType);
                return true;
            }
        }

        type = null;
        return false;
    }

    public static bool CanSupportType(Type type)
    {
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

    public static object? Deserialize(IValue value, Type type)
        => Deserialize(value, type, ModelOptions.Default);

    public static object? Deserialize(IValue value, Type type, ModelOptions options)
    {
        if (typeof(IValue).IsAssignableFrom(type))
        {
            return value;
        }

        if (type.IsInstanceOfType(value))
        {
            return value;
        }

        if (TypeDescriptor.GetConverter(type) is TypeConverter converter && converter.CanConvertFrom(value.GetType()))
        {
            return converter.ConvertFrom(value);
        }

        if (IsStandardType(type) || IsStandardArrayType(type))
        {
            return DeserializeValue(type, value, options);
        }

        var data = ModelData.GetObject(value);
        var header = data.Header;

        if (header.TypeValue != ObjectValue)
        {
            throw new ModelSerializationException(
                $"Given magic value {header.TypeValue} is not {ObjectValue}");
        }

        var headerType = Type.GetType(header.TypeName);
        if (headerType != type && type.IsAssignableFrom(headerType) is false)
        {
            throw new ModelSerializationException($"Given type {headerType} is not {type}");
        }

        if (data.Value is not List list)
        {
            throw new ModelSerializationException($"The value is not a list: {data.Value}");
        }

        var modelType = options.GetType(headerType, header.Version);
        var modelVersion = header.Version;
        var currentVersion = options.GetVersion(headerType);

        var obj = CreateInstance(modelType);
        var propertyInfos = options.GetProperties(modelType);
        for (var i = 0; i < propertyInfos.Length; i++)
        {
            var propertyInfo = propertyInfos[i];
            var propertyAttribute = propertyInfo.GetCustomAttribute<PropertyAttribute>()
                ?? throw new UnreachableException(
                    "Property does not have SerializablePropertyAttribute");
            var propertyIndex = propertyAttribute.Index;
            var propertyType = propertyInfo.PropertyType;
            var propertyValue = list[propertyIndex];
            var deserializedValue = propertyValue is null ? null : Deserialize(propertyValue, propertyType, options);
            propertyInfo.SetValue(obj, deserializedValue);
        }

        while (modelVersion < currentVersion)
        {
            var args = new object[] { obj };
            modelType = options.GetType(headerType, modelVersion + 1);
            obj = CreateInstance(modelType, args: args);
            modelVersion++;
        }

        return obj;
    }

    public static T Deserialize<T>(IValue value)
        => Deserialize<T>(value, ModelOptions.Default);

    public static T Deserialize<T>(IValue value, ModelOptions options)
    {
        if (Deserialize(value, typeof(T), options) is T obj)
        {
            return obj;
        }

        throw new ModelSerializationException(
            $"Failed to deserialize {typeof(T)} from {value.Inspect()}.");
    }

    public static T DeserializeFromBytes<T>(byte[] bytes)
    {
        return Deserialize<T>(_codec.Decode(bytes))
            ?? throw new ModelSerializationException(
                $"Failed to deserialize {typeof(T)} from bytes.");
    }

    private static IValue Serialize(object obj, Type type, ModelOptions options)
    {
        if (obj is IValue v)
        {
            return v;
        }

        if (TypeDescriptor.GetConverter(type) is TypeConverter converter && converter.CanConvertTo(typeof(IValue)))
        {
            return converter.ConvertTo(obj, typeof(IValue)) is IValue value
                ? value : throw new ModelSerializationException($"Failed to convert {obj} to {type}");
        }

        if (IsStandardType(type) || IsStandardArrayType(type))
        {
            return SerializeValue(obj, type, options);
        }

        var typeName = options.GetTypeName(type);
        var version = options.GetVersion(type);
        var propertyInfos = options.GetProperties(type);
        var valueList = new List<IValue>(propertyInfos.Length);
        foreach (var propertyInfo in propertyInfos)
        {
            var value = propertyInfo.GetValue(obj);
            var propertyType = propertyInfo.PropertyType;
            var serialized = value is not null ? Serialize(value, propertyType, options) : Null.Value;
            valueList.Add(serialized);
        }

        var data = new ModelData
        {
            Header = new ModelHeader
            {
                TypeValue = ObjectValue,
                TypeName = typeName,
                Version = version,
            },
            Value = new List(valueList),
        };
        return data.Bencoded;
    }

    private static IValue SerializeValue(
        object? value, Type propertyType, ModelOptions options)
    {
        if (value is IBencodable bencodable)
        {
            return bencodable.Bencoded;
        }
        else if (value is IValue bencoded)
        {
            return bencoded;
        }
        else if (value is null)
        {
            return Null.Value;
        }
        else if (propertyType.IsEnum)
        {
            var underlyingType = Enum.GetUnderlyingType(propertyType);
            if (underlyingType == typeof(int))
            {
                return new Integer((int)value);
            }
            else if (underlyingType == typeof(long))
            {
                return new Integer((long)value);
            }
        }
        else if (IsArray(propertyType, out var elementType))
        {
            var items = (IList)value;
            if (items.Count == 0)
            {
                return Null.Value;
            }

            var list = new List<IValue>(items.Count);
            var typeName = options.GetTypeName(elementType);
            var version = options.GetVersion(elementType);

            foreach (var item in items)
            {
                list.Add(Serialize(item, elementType, options));
            }

            var data = new ModelData
            {
                Header = new ModelHeader
                {
                    TypeValue = ArrayValue,
                    TypeName = typeName,
                    Version = version,
                },
                Value = new List(list),
            };

            return data.Bencoded;
        }
        else if (IsImmutableArray(propertyType, out elementType))
        {
            var items = (IList)value;
            if (items.Count == 0)
            {
                return Null.Value;
            }

            var list = new List<IValue>(items.Count);
            var typeName = options.GetTypeName(elementType);
            var version = options.GetVersion(elementType);

            foreach (var item in items)
            {
                list.Add(Serialize(item, elementType, options));
            }

            var data = new ModelData
            {
                Header = new ModelHeader
                {
                    TypeValue = ImmutableArrayValue,
                    TypeName = typeName,
                    Version = version,
                },
                Value = new List(list),
            };

            return data.Bencoded;
        }
        else if (IsImmutableSortedSet(propertyType, out elementType))
        {
            var items = (IList)value;
            if (items.Count == 0)
            {
                return Null.Value;
            }

            var list = new List<IValue>(items.Count);
            var typeName = options.GetTypeName(elementType);
            var version = options.GetVersion(elementType);

            foreach (var item in items)
            {
                list.Add(Serialize(item, elementType, options));
            }

            var data = new ModelData
            {
                Header = new ModelHeader
                {
                    TypeValue = ImmutableSortedSetValue,
                    TypeName = typeName,
                    Version = version,
                },
                Value = new List(list),
            };

            return data.Bencoded;
        }
        else if (propertyType.IsDefined(typeof(ModelAttribute)))
        {
            return Serialize(value);
        }

        throw new ModelSerializationException($"Unsupported type {value.GetType()}");
    }

    private static object? DeserializeValue(
        Type propertyType, IValue propertyValue, ModelOptions options)
    {
        if (IsBencodableType(propertyType))
        {
            return CreateInstance(propertyType, args: [propertyValue]);
        }
        else if (IsBencodexType(propertyType))
        {
            if (propertyValue.GetType() == propertyType)
            {
                return propertyValue;
            }
        }
        else if (propertyType.IsEnum)
        {
            if (propertyValue is Integer integer)
            {
                var underlyingType = Enum.GetUnderlyingType(propertyType);
                if (underlyingType == typeof(long))
                {
                    return Enum.ToObject(propertyType, (long)integer.Value);
                }
                else if (underlyingType == typeof(int))
                {
                    return Enum.ToObject(propertyType, (int)integer.Value);
                }
            }
        }
        else if (propertyType.IsDefined(typeof(ModelAttribute)))
        {
            if (propertyValue is Null)
            {
                return null;
            }

            return Deserialize(propertyValue, propertyType);
        }
        else if (IsArray(propertyType, out var elementType))
        {
            if (propertyValue is Null)
            {
                return ToEmptyArray(elementType);
            }

            if (ModelData.TryGetObject(propertyValue, out var data))
            {
                var header = data.Header;
                if (header.TypeValue != ArrayValue)
                {
                    throw new ModelSerializationException(
                        $"Given magic value {header.TypeValue} is not {ArrayValue}");
                }

                var typeName = options.GetTypeName(elementType);
                if (header.TypeName != typeName)
                {
                    throw new ModelSerializationException(
                        $"Given type name {header.TypeName} is not {typeName}");
                }

                var list = (List)data.Value;
                return ToArray(list, elementType, options);
            }
        }
        else if (IsImmutableArray(propertyType, out elementType))
        {
            if (propertyValue is Null)
            {
                return ToImmutableEmptyArray(elementType);
            }

            if (ModelData.TryGetObject(propertyValue, out var data))
            {
                var header = data.Header;
                if (header.TypeValue != ImmutableArrayValue)
                {
                    throw new ModelSerializationException(
                        $"Given magic value {header.TypeValue} is not {ArrayValue}");
                }

                var typeName = options.GetTypeName(elementType);
                if (header.TypeName != typeName)
                {
                    throw new ModelSerializationException(
                        $"Given type name {header.TypeName} is not {typeName}");
                }

                var list = (List)data.Value;
                return ToImmutableArray(list, elementType, options);
            }
        }
        else if (IsImmutableSortedSet(propertyType, out elementType))
        {
            if (propertyValue is Null)
            {
                return ToImmutableEmptySortedSet(elementType);
            }

            if (ModelData.TryGetObject(propertyValue, out var data))
            {
                var header = data.Header;
                if (header.TypeValue != ImmutableSortedSetValue)
                {
                    throw new ModelSerializationException(
                        $"Given magic value {header.TypeValue} is not {ArrayValue}");
                }

                var typeName = options.GetTypeName(elementType);
                if (header.TypeName != typeName)
                {
                    throw new ModelSerializationException(
                        $"Given type name {header.TypeName} is not {typeName}");
                }

                var list = (List)data.Value;
                return ToImmutableSortedSet(list, elementType, options);
            }
        }
        else if (propertyValue is Null)
        {
            return null;
        }
        else
        {
            var message = $"Unsupported type {propertyType}. Cannot convert value of type " +
                          $"{propertyValue.GetType()} to {propertyType}";
            throw new ModelSerializationException(message);
        }

        throw new ModelSerializationException(
            $"Unsupported type {propertyType}. Cannot convert value of type " +
            $"{propertyValue.GetType()} to {propertyType}");
    }

    private static Array ToArray(List list, Type elementType, ModelOptions options)
    {
        var array = Array.CreateInstance(elementType, list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i];
            var itemValue = item is null ? null : Deserialize(item, elementType, options);
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
            var itemValue = item is null ? null : Deserialize(item, elementType, options);
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
            var itemValue = item is null ? null : Deserialize(item, elementType, options);
            listInstance.Add(itemValue);
        }

        var methodName = nameof(ImmutableSortedSet.CreateRange);
        var methodInfo = GetCreateRangeMethod(
            typeof(ImmutableSortedSet), methodName, typeof(IEnumerable<>));
        var genericMethodInfo = methodInfo.MakeGenericMethod(elementType);
        var methodArgs = new object?[] { listInstance };
        return genericMethodInfo.Invoke(null, parameters: methodArgs)!;
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
}
