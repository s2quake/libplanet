using System.Collections;
using System.Diagnostics;

namespace Libplanet.Serialization.Json.DynamicConverters;

internal sealed class ArrayJsonConverter : CollectionJsonConverter
{
    protected override Type GenericTypeDefinition
        => throw new UnreachableException("Array does not have a generic type definition.");

    protected override bool IsDictionary => false;

    public override bool CanConvert(Type typeToConvert) => typeof(Array).IsAssignableFrom(typeToConvert);

    protected override IEnumerable CreateInstance(Type typeToConvert, Type elementType, IList listInstance)
    {
        var array = Array.CreateInstance(typeToConvert.GetElementType()!, listInstance.Count);
        listInstance.CopyTo(array, 0);
        return array;
    }

    protected override Type GetElementType(Type typeToConvert) => typeToConvert.GetElementType()
        ?? throw new ArgumentException($"Cannot get the element type from {typeToConvert}.", nameof(typeToConvert));
}
