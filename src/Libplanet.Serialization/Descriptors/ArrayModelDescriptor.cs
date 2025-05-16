using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Serialization.Descriptors;

internal sealed class ArrayModelDescriptor : ModelDescriptor
{
    public override bool CanSerialize(Type type) => IsArray(type);

    public override Type[] GetTypes(Type type, out bool isArray)
    {
        isArray = true;
        return [type.GetElementType()!];
    }

    public override object?[] GetValues(object obj, Type type)
    {
        if (obj is IList items)
        {
            var values = new object?[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                values[i] = items[i];
            }

            return values;
        }
        else
        {
            throw new InvalidOperationException($"Cannot get values from {obj.GetType()}");
        }
    }

    public override object CreateInstance(Type type, object?[] values)
    {
        var array = Array.CreateInstance(type.GetElementType()!, values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            array.SetValue(values[i], i);
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
