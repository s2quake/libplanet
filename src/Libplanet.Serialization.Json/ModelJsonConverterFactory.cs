using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Libplanet.Serialization.Json.Converters;
using Libplanet.Serialization.Json.CollectionConverters;

namespace Libplanet.Serialization.Json;

public sealed class ModelJsonConverterFactory(ModelOptions options) : JsonConverterFactory
{
    private static readonly object _lock = new();
    private static readonly Dictionary<Type, JsonConverter> _converterByType = new()
    {
        [typeof(BigInteger)] = new BigIntegerJsonConverter(),
        [typeof(bool)] = new BooleanJsonConverter(),
        [typeof(byte)] = new ByteJsonConverter(),
        [typeof(char)] = new CharJsonConverter(),
        [typeof(DateTimeOffset)] = new DateTimeOffsetJsonConverter(),
        [typeof(Guid)] = new GuidJsonConverter(),
        [typeof(int)] = new Int32JsonConverter(),
        [typeof(long)] = new Int64JsonConverter(),
        [typeof(string)] = new StringJsonConverter(),
        [typeof(TimeSpan)] = new TimeSpanJsonConverter(),
    };
    private static readonly JsonConverter[] _descriptors =
    [
        new NullableJsonConverter(),
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
    private static readonly Dictionary<Type, JsonConverter> _descriptorByType = [];

    private readonly ObjectJsonConverter _objectJsonConverter = new(options);

    public override bool CanConvert(Type typeToConvert)
    {
        if (typeToConvert == typeof(object) || typeToConvert.IsDefined(typeof(ModelAttribute), inherit: false))
        {
            return true;
        }

        if (_converterByType.TryGetValue(typeToConvert, out _))
        {
            return true;
        }

        if (TryGetConverter(typeToConvert, out _))
        {
            return true;
        }

        return false;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(object) || typeToConvert.IsDefined(typeof(ModelAttribute), inherit: false))
        {
            return _objectJsonConverter;
        }

        if (_converterByType.TryGetValue(typeToConvert, out var c0))
        {
            return c0;
        }

        if (TryGetConverter(typeToConvert, out var c1))
        {
            return c1;
        }

        return null;
    }

    private static bool TryGetConverter(Type type, [MaybeNullWhen(false)] out JsonConverter descriptor)
    {
        lock (_lock)
        {
            if (FindDescriptor(type) is { } foundDescriptor)
            {
                descriptor = foundDescriptor;
                return true;
            }

            descriptor = null;
            return descriptor is not null;
        }

        static JsonConverter? FindDescriptor(Type type)
        {
            if (_descriptorByType.TryGetValue(type, out var descriptor))
            {
                return descriptor;
            }

            descriptor = _descriptors.FirstOrDefault(descriptor => descriptor.CanConvert(type));
            if (descriptor is not null)
            {
                _descriptorByType.TryAdd(type, descriptor);
                return descriptor;
            }

            return null;
        }
    }
}
