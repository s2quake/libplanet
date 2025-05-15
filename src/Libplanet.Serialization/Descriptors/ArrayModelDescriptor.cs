using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Serialization.Descriptors;

internal sealed class ArrayModelDescriptor : ModelDescriptor
{
    public override bool CanSerialize(Type type) => IsArray(type);

    public override IEnumerable<Type> GetTypes(Type type, int length)
    {
        var elementType = type.GetElementType()!;
        for (var i = 0; i < length; i++)
        {
            yield return elementType;
        }
    }

    public override IEnumerable<(Type Type, object? Value)> GetValues(object obj, Type type)
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

    public override object CreateInstance(Type type, IEnumerable<object?> values)
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

    public override bool Equals(object obj1, object obj2, Type type)
    {
        var array1 = (Array)obj1;
        var array2 = (Array)obj2;
        if (array1.GetType() != array2.GetType())
        {
            return false;
        }

        if (array1.Length != array2.Length)
        {
            return false;
        }

        var elementType = type.GetElementType()!;
        for (var i = 0; i < array1.Length; i++)
        {
            if (!ModelResolver.Equals(array1.GetValue(i), array2.GetValue(i), elementType))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode(object obj, Type type)
    {
        var items = (Array)obj;
        var elementType = type.GetElementType()!;
        HashCode hash = default;
        foreach (var item in items)
        {
            hash.Add(ModelResolver.GetHashCode(item, elementType));
        }

        return hash.ToHashCode();
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
