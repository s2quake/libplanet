using System.Collections;
using System.Diagnostics;

namespace Libplanet.Serialization.Descriptors;

internal sealed class ArrayModelDescriptor : CollectionModelDescriptor
{
    protected override Type GenericTypeDefinition
        => throw new UnreachableException("Array does not have a generic type definition.");

    protected override bool IsDictionary => false;

    public override bool CanSerialize(Type type) => typeof(Array).IsAssignableFrom(type);

    protected override IEnumerable CreateInstance(Type type, Type elementType, IList listInstance)
    {
        var array = Array.CreateInstance(type.GetElementType()!, listInstance.Count);
        listInstance.CopyTo(array, 0);
        return array;
    }

    protected override Type GetElementType(Type type) => type.GetElementType()
        ?? throw new ArgumentException($"Cannot get the element type from {type}.", nameof(type));
}
