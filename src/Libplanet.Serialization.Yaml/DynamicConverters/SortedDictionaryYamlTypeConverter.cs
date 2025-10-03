using System.Collections;

namespace Libplanet.Serialization.Yaml.DynamicConverters;

internal sealed class SortedDictionaryYamlTypeConverter : CollectionYamlTypeConverter
{
    protected override Type GenericTypeDefinition => typeof(SortedDictionary<,>);

    protected override bool IsDictionary => true;

    protected override IEnumerable CreateInstance(Type typeToConvert, Type elementType, IList listInstance)
    {
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(elementType.GetGenericArguments());
        var dictionary = (ICollection)Activator.CreateInstance(dictionaryType, [listInstance])!;
        return (IEnumerable)Activator.CreateInstance(typeToConvert, [dictionary])!;
    }
}
