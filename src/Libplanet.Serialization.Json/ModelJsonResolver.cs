using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Libplanet.Serialization.Json.StaticConverters;
using Libplanet.Serialization.Json.DynamicConverters;
using System.ComponentModel;

namespace Libplanet.Serialization.Json;

internal static class ModelJsonResolver
{
    private static readonly object _lock = new();
    private static readonly Dictionary<Type, JsonConverter> _converterByType = [];

    private static readonly JsonConverter[] _staticConverters =
    [
        new BigIntegerJsonConverter(),
        new BooleanJsonConverter(),
        new ByteJsonConverter(),
        new CharJsonConverter(),
        new DateTimeOffsetJsonConverter(),
        new GuidJsonConverter(),
        new Int32JsonConverter(),
        new Int64JsonConverter(),
        new StringJsonConverter(),
        new TimeSpanJsonConverter(),
        new ByteArrayJsonConverter(),
        new ImmutableByteArrayJsonConverter(),
    ];

    private static readonly JsonConverter[] _dynamicConverters =
    [
        new EnumJsonConverter(),
        new ModelObjectJsonConverter(),
        new ScalarValueJsonConverter(),
        new TupleJsonConverter(),
        new KeyValuePairJsonConverter(),
        new ArrayJsonConverter(),
        new ListJsonConverter(),
        new HashSetJsonConverter(),
        new SortedSetJsonConverter(),
        new DictionaryJsonConverter(),
        new SortedDictionaryJsonConverter(),
        new ImmutableArrayJsonConverter(),
        new ImmutableListJsonConverter(),
        new ImmutableHashSetJsonConverter(),
        new ImmutableSortedSetJsonConverter(),
        new ImmutableDictionaryJsonConverter(),
        new ImmutableSortedDictionaryJsonConverter(),
    ];

    private static readonly NullableJsonConverter _nullableConverter = new();

    public static bool TryGetConverter(Type type, [MaybeNullWhen(false)] out JsonConverter converter)
    {
        lock (_lock)
        {
            if (FindConverter(type) is { } foundConverter)
            {
                converter = foundConverter;
                return true;
            }

            converter = null;
            return converter is not null;
        }
    }

    private static JsonConverter? FindConverter(Type type)
    {
        if (_converterByType.TryGetValue(type, out var converter))
        {
            return converter;
        }

        if (_nullableConverter.CanConvert(type))
        {
            converter = _nullableConverter;
            _converterByType.TryAdd(type, converter);
            return converter;
        }

        converter = _staticConverters.FirstOrDefault(converter => converter.CanConvert(type))
            ?? _dynamicConverters.FirstOrDefault(converter => converter.CanConvert(type));
        if (converter is not null)
        {
            var genericConverterType = GetGenericConverterType(converter.GetType());
            if (genericConverterType is not null)
            {
                var genericType = genericConverterType.GetGenericArguments()[0];
                var wrappedConverter = (JsonConverter)typeof(WrappedJsonConverter<>)
                    .MakeGenericType(genericType)
                    .GetConstructor([genericConverterType])!
                    .Invoke([converter]);
                converter = wrappedConverter;
            }

            _converterByType.TryAdd(type, converter);
            return converter;
        }

        return null;
    }

    private static Type? GetGenericConverterType(Type converterType)
    {
        var type = converterType;
        while (type != typeof(object))
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(JsonConverter<>))
            {
                return type;
            }

            type = type.BaseType!;
        }

        return null;
    }
}
