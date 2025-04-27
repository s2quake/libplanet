using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Bencodex.Types;
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

        var typeName = options.GetTypeName(type);
        if (header.TypeName != typeName)
        {
            throw new ModelSerializationException(
                $"Given type name {header.TypeName} is not {typeName}");
        }

        if (data.Value is not List list)
        {
            throw new ModelSerializationException(
                $"The value is not a list: {data.Value}");
        }

        var modelType = options.GetType(type, header.Version);
        var modelVersion = header.Version;
        var currentVersion = options.GetVersion(type);

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
            var deserializedValue = DeserializeValue(propertyType, propertyValue, options);
            propertyInfo.SetValue(obj, deserializedValue);
        }

        while (modelVersion < currentVersion)
        {
            var args = new object[] { obj };
            modelType = options.GetType(type, modelVersion + 1);
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
        else if (value is int @int)
        {
            return new Integer(@int);
        }
        else if (value is long @long)
        {
            return new Integer(@long);
        }
        else if (value is BigInteger bigInteger)
        {
            return new Integer(bigInteger);
        }
        else if (value is string @string)
        {
            return new Text(@string);
        }
        else if (value is bool @bool)
        {
            return new Bencodex.Types.Boolean(@bool);
        }
        else if (value is byte[] bytes)
        {
            return new Binary(bytes);
        }
        else if (value is DateTimeOffset dateTimeOffset)
        {
            return new Integer(dateTimeOffset.UtcTicks);
        }
        else if (value is TimeSpan timeSpan)
        {
            return new Integer(timeSpan.Ticks);
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
                list.Add(SerializeValue(item, elementType, options));
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
                list.Add(SerializeValue(item, elementType, options));
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
                list.Add(SerializeValue(item, elementType, options));
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
        else if (propertyType == typeof(int))
        {
            if (propertyValue is Integer integer)
            {
                return (int)integer.Value;
            }
        }
        else if (propertyType == typeof(long))
        {
            if (propertyValue is Integer integer)
            {
                return (long)integer.Value;
            }
        }
        else if (propertyType == typeof(BigInteger))
        {
            if (propertyValue is Integer integer)
            {
                return integer.Value;
            }
        }
        else if (propertyType == typeof(string))
        {
            if (propertyValue is Text text)
            {
                return text.Value;
            }
            else if (propertyValue is Null)
            {
                return null;
            }
        }
        else if (propertyType == typeof(bool))
        {
            if (propertyValue is Bencodex.Types.Boolean boolean)
            {
                return boolean.Value;
            }
        }
        else if (propertyType == typeof(byte[]))
        {
            if (propertyValue is Binary binary)
            {
                return binary.ToByteArray();
            }
            else if (propertyValue is Null)
            {
                return null;
            }
        }
        else if (propertyType == typeof(DateTimeOffset))
        {
            if (propertyValue is Integer integer)
            {
                return new DateTimeOffset((long)integer.Value, TimeSpan.Zero);
            }
        }
        else if (propertyType == typeof(TimeSpan))
        {
            if (propertyValue is Integer integer)
            {
                return new TimeSpan((long)integer.Value);
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
            var itemValue = item is null ? null : DeserializeValue(elementType, item, options);
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
            var itemValue = item is null ? null : DeserializeValue(elementType, item, options);
            listInstance.Add(itemValue);
        }

        var methodName = nameof(ImmutableArray.CreateRange);
        var methodInfo = GetCreateRangeMethod(
            typeof(ImmutableArray), methodName, typeof(IEnumerable<>));
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
