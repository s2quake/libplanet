using System.Collections;

namespace Libplanet.Serialization.DynamicConverters;

internal sealed class SortedDictionaryModelConverter : CollectionModelConverter
{
    protected override Type GenericTypeDefinition => typeof(SortedDictionary<,>);

    protected override bool IsDictionary => true;

    protected override IEnumerable CreateInstance(Type type, Type elementType, IList listInstance)
    {
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(elementType.GetGenericArguments());
        var dictionary = (ICollection)Activator.CreateInstance(dictionaryType, [listInstance])!;
        return (IEnumerable)Activator.CreateInstance(type, [dictionary])!;
    }
}
