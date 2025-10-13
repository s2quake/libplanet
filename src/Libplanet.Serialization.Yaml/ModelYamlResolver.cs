using System.Diagnostics.CodeAnalysis;
using Libplanet.Serialization.Yaml.StaticConverters;
using Libplanet.Serialization.Yaml.DynamicConverters;
using YamlDotNet.Serialization;

namespace Libplanet.Serialization.Yaml;

internal static class ModelYamlResolver
{
    private static readonly object _lock = new();
    private static readonly Dictionary<Type, IYamlTypeConverter> _converterByType = [];

    private static readonly IYamlTypeConverter[] _staticConverters =
    [
        new BigIntegerYamlTypeConverter(),
        new BooleanYamlTypeConverter(),
        new ByteYamlTypeConverter(),
        new CharYamlTypeConverter(),
        new DateTimeOffsetYamlTypeConverter(),
        new GuidYamlTypeConverter(),
        new Int32YamlTypeConverter(),
        new Int64YamlTypeConverter(),
        new StringYamlTypeConverter(),
        new TimeSpanYamlTypeConverter(),
        new ByteArrayYamlTypeConverter(),
        new ImmutableByteArrayYamlTypeConverter(),
    ];

    private static readonly IYamlTypeConverter[] _dynamicConverters =
    [
        new NullableYamlTypeConverter(),
        new EnumYamlTypeConverter(),
        new ModelObjectYamlTypeConverter(),
        new ScalarValueYamlTypeConverter(),
        new TupleYamlTypeConverter(),
        new KeyValuePairYamlTypeConverter(),
        new ArrayYamlTypeConverter(),
        new ListYamlTypeConverter(),
        new HashSetYamlTypeConverter(),
        new SortedSetYamlTypeConverter(),
        new DictionaryYamlTypeConverter(),
        new SortedDictionaryYamlTypeConverter(),
        new ImmutableArrayYamlTypeConverter(),
        new ImmutableListYamlTypeConverter(),
        new ImmutableHashSetYamlTypeConverter(),
        new ImmutableSortedSetYamlTypeConverter(),
        new ImmutableDictionaryYamlTypeConverter(),
        new ImmutableSortedDictionaryYamlTypeConverter(),
    ];

    public static bool TryGetConverter(Type type, [MaybeNullWhen(false)] out IYamlTypeConverter converter)
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

    private static IYamlTypeConverter? FindConverter(Type type)
    {
        if (_converterByType.TryGetValue(type, out var converter))
        {
            return converter;
        }

        converter = _staticConverters.FirstOrDefault(converter => converter.Accepts(type))
            ?? _dynamicConverters.FirstOrDefault(converter => converter.Accepts(type));
        if (converter is not null)
        {
            _converterByType.TryAdd(type, converter);
            return converter;
        }

        return null;
    }
}
