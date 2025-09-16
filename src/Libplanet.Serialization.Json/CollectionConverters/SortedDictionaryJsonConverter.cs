using System.Collections;

namespace Libplanet.Serialization.Json.CollectionConverters;

internal sealed class SortedDictionaryJsonConverter : CollectionJsonConverter
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
