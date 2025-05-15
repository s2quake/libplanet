using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Serialization.Descriptors;

internal sealed class ArrayModelDescriptor : ModelDescriptorBase
{
    public override bool CanSerialize(Type type) => IsArray(type);

    public override bool CanDeserialize(Type type) => IsArray(type);

    public override IEnumerable<Type> GetTypes(Type type, int length, ModelOptions options)
    {
        var elementType = type.GetElementType()!;
        for (var i = 0; i < length; i++)
        {
            yield return elementType;
        }
    }

    public override IEnumerable<(Type Type, object? Value)> GetValues(object obj, Type type, ModelOptions options)
    {
        if (obj is IList items)
        {
            var elementType = items.GetType().GetElementType()!;
            for (var i = 0; i < items.Count; i++)
            {
                yield return (elementType, items[i]);
            }
        }
    }

    public override object CreateInstance(Type type, IEnumerable<object?> values, ModelOptions options)
    {
        var array = Array.CreateInstance(type.GetElementType()!, values.Count());
        var i = 0;
        var enumerator = values.GetEnumerator();
        while (enumerator.MoveNext())
        {
            array.SetValue(enumerator.Current, i++);
        }

        return array;
    }

    private static bool IsArray(Type type) => IsArray(type, out _);

    private static bool IsArray(Type type, [MaybeNullWhen(false)] out Type elementType)
    {
        if (typeof(Array).IsAssignableFrom(type))
        {
            elementType = type.GetElementType()!;
            return true;
        }

        elementType = null;
        return false;
    }
}
