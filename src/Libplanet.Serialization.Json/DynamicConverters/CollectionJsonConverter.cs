using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Libplanet.Serialization.Json.DynamicConverters;

internal abstract class CollectionJsonConverter : JsonConverter<IEnumerable>
{
    private const string KeyName = nameof(KeyValuePair<object, object>.Key);
    private const string ValueName = nameof(KeyValuePair<object, object>.Value);

    protected abstract Type GenericTypeDefinition { get; }

    protected abstract bool IsDictionary { get; }

    public override IEnumerable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var elementType = GetElementType(typeToConvert);
        if (IsDictionary && CanKeyConvertToString(elementType.GetGenericArguments()[0]))
        {
            var keyType = elementType.GetGenericArguments()[0];
            var valueType = typeToConvert.GetGenericArguments()[1];
            var listInstance = CreateListInstance(elementType);
            reader.Expect(JsonTokenType.StartObject);
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                var propertyName = reader.ReadPropertyName();
                var key = ConvertFromPropertyName(propertyName, keyType);
                using var _ = ModelTypeScope.Push(valueType);
                var value = JsonSerializer.Deserialize(ref reader, valueType, options);
                var kv = TypeUtility.CreateInstance(elementType, args: [key, value]);
                listInstance.Add(kv);
            }

            reader.Expect(JsonTokenType.EndObject);
            return CreateInstance(typeToConvert, elementType, listInstance);
        }
        else
        {
            var listInstance = CreateListInstance(elementType);
            using var _ = ModelTypeScope.Push(elementType);
            reader.Expect(JsonTokenType.StartArray);
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var value = JsonSerializer.Deserialize(ref reader, elementType, options);
                listInstance.Add(value);
            }

            reader.Expect(JsonTokenType.EndArray);
            return CreateInstance(typeToConvert, elementType, listInstance);
        }
    }

    public override void Write(Utf8JsonWriter writer, IEnumerable value, JsonSerializerOptions options)
    {
        var elementType = GetElementType(value.GetType());
        if (IsDictionary && CanKeyConvertToString(elementType.GetGenericArguments()[0]))
        {
            writer.WriteStartObject();
            var keyProperty = GetPropertyInfo(elementType, KeyName);
            var enumerator = value.GetEnumerator();
            var valueProperty = GetPropertyInfo(elementType, ValueName);
            while (enumerator.MoveNext())
            {
                var kv = enumerator.Current;
                var keyValue = keyProperty.GetValue(kv)
                    ?? throw new InvalidOperationException("Key cannot be null.");
                var keyString = ConvertToPropertyName(keyValue);
                writer.WritePropertyName(keyString);

                var valueValue = valueProperty.GetValue(kv);
                var valueActualType = TypeUtility.GetActualType(valueValue, valueProperty.PropertyType);
                using var _ = ModelTypeScope.Push(valueProperty.PropertyType);
                JsonSerializer.Serialize(writer, valueValue, valueActualType, options);
            }

            writer.WriteEndObject();
        }
        else
        {
            writer.WriteStartArray();
            var enumerator = value.GetEnumerator();
            using var _ = ModelTypeScope.Push(elementType);
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                var currentType = TypeUtility.GetActualType(current, elementType);
                JsonSerializer.Serialize(writer, current, currentType, options);
            }

            writer.WriteEndArray();
        }
    }

    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == GenericTypeDefinition;

    protected virtual IEnumerable CreateInstance(Type typeToConvert, Type elementType, IList listInstance)
        => (IEnumerable)TypeUtility.CreateInstance(typeToConvert, args: [listInstance]);

    protected virtual Type GetElementType(Type typeToConvert)
    {
        if (CanConvert(typeToConvert))
        {
            if (IsDictionary)
            {
                return typeof(KeyValuePair<,>).MakeGenericType(typeToConvert.GetGenericArguments());
            }

            return typeToConvert.GetGenericArguments()[0];
        }

        throw new NotSupportedException("The type is not supported.");
    }

    private static IList CreateListInstance(Type elementType)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        return (IList)TypeUtility.CreateInstance(listType, args: [])!;
    }

    private static PropertyInfo GetPropertyInfo(Type type, string propertyName)
        => type.GetProperty(propertyName) ?? throw new UnreachableException($"{propertyName} property not found.");

    private static bool CanKeyConvertToString(Type type)
    {
        if (type.IsDefined(typeof(ModelScalarAttribute), inherit: false))
        {
            return true;
        }

        return type == typeof(BigInteger) ||
               type == typeof(bool) ||
               type == typeof(byte[]) ||
               type == typeof(byte) ||
               type == typeof(char) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(Guid) ||
               type == typeof(ImmutableArray<byte>) ||
               type == typeof(int) ||
               type == typeof(long) ||
               type == typeof(string) ||
               type == typeof(TimeSpan) ||
               type.IsEnum;
    }

    private static string ConvertToPropertyName(object value)
    {
        if (value is BigInteger bigInteger)
        {
            return bigInteger.ToString("D", NumberFormatInfo.InvariantInfo);
        }
        else if (value is bool @bool)
        {
            return @bool.ToString();
        }
        else if (value is byte[] bytes)
        {
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
        else if (value is byte @byte)
        {
            return @byte.ToString();
        }
        else if (value is char @char)
        {
            return @char.ToString();
        }
        else if (value is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString("o", CultureInfo.InvariantCulture);
        }
        else if (value is Guid guid)
        {
            return guid.ToString("D");
        }
        else if (value is ImmutableArray<byte> immutableArray)
        {
            return Convert.ToHexString([.. immutableArray]).ToLowerInvariant();
        }
        else if (value is int @int)
        {
            return @int.ToString();
        }
        else if (value is long @long)
        {
            return @long.ToString();
        }
        else if (value is string @string)
        {
            return @string;
        }
        else if (value is TimeSpan timeSpan)
        {
            return timeSpan.Ticks.ToString();
        }
        else if (value is Enum @enum)
        {
            return @enum.ToString();
        }
        else if (value.GetType().GetCustomAttribute<ModelScalarAttribute>() is { } attribute)
        {
            var kind = attribute.Kind;
            var scalarValue = ModelScalarUtility.GetScalarValue(value);
            return kind switch
            {
                ModelScalarKind.String => (string)scalarValue,
                ModelScalarKind.Boolean => ((bool)scalarValue).ToString(),
                ModelScalarKind.Int32 => ((int)scalarValue).ToString(),
                ModelScalarKind.Int64 => ((long)scalarValue).ToString(),
                ModelScalarKind.Hex => Convert.ToHexString((byte[])scalarValue).ToLowerInvariant(),
                _ => throw new NotSupportedException("The scalar value is of an unsupported type."),
            };
        }
        else
        {
            throw new NotSupportedException("The property type is of an unsupported type.");
        }
    }

    private static object ConvertFromPropertyName(string propertyName, Type type)
    {
        if (type == typeof(BigInteger))
        {
            return BigInteger.Parse(propertyName, NumberFormatInfo.InvariantInfo);
        }
        else if (type == typeof(bool))
        {
            return bool.Parse(propertyName);
        }
        else if (type == typeof(byte[]))
        {
            return Convert.FromHexString(propertyName);
        }
        else if (type == typeof(byte))
        {
            return byte.Parse(propertyName);
        }
        else if (type == typeof(char))
        {
            return char.Parse(propertyName);
        }
        else if (type == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(propertyName, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
        else if (type == typeof(Guid))
        {
            return Guid.Parse(propertyName);
        }
        else if (type == typeof(ImmutableArray<byte>))
        {
            return ImmutableArray.Create(Convert.FromHexString(propertyName));
        }
        else if (type == typeof(int))
        {
            return int.Parse(propertyName);
        }
        else if (type == typeof(long))
        {
            return long.Parse(propertyName);
        }
        else if (type == typeof(string))
        {
            return propertyName;
        }
        else if (type == typeof(TimeSpan))
        {
            return new TimeSpan(long.Parse(propertyName));
        }
        else if (type.IsEnum)
        {
            return Enum.Parse(type, propertyName);
        }
        else if (type.GetCustomAttribute<ModelScalarAttribute>() is { } attribute)
        {
            var kind = attribute.Kind;
            object scalarValue = kind switch
            {
                ModelScalarKind.String => propertyName,
                ModelScalarKind.Boolean => bool.Parse(propertyName),
                ModelScalarKind.Int32 => int.Parse(propertyName),
                ModelScalarKind.Int64 => long.Parse(propertyName),
                ModelScalarKind.Hex => Convert.FromHexString(propertyName),
                _ => throw new NotSupportedException("The scalar value is of an unsupported type."),
            };
            return ModelScalarUtility.GetObjectFromScalarValue(type, scalarValue);
        }
        else
        {
            throw new NotSupportedException("The property type is of an unsupported type.");
        }
    }
}
