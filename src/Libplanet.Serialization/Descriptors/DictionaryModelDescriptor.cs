using System.Collections;
using System.Diagnostics;

namespace Libplanet.Serialization.Descriptors;

internal sealed class DictionaryModelDescriptor : ModelDescriptor
{
    public override bool CanSerialize(Type type) => IsDictionary(type);

    public override Type[] GetTypes(Type type, out bool isArray)
    {
        isArray = true;
        return [GetElementType(type)];
    }

    public override object?[] GetValues(object obj, Type type)
    {
        if (obj is ICollection items)
        {
            var values = new object?[items.Count];
            var i = 0;
            var enumerator = items.GetEnumerator();
            while (enumerator.MoveNext())
            {
                values[i++] = enumerator.Current;
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
        var elementType = GetElementType(type);
        var listType = typeof(List<>).MakeGenericType(elementType);
        var listInstance = (IList)TypeUtility.CreateInstance(listType, args: [values.Length])!;
        foreach (var value in values)
        {
            listInstance.Add(value);
        }

        return TypeUtility.CreateInstance(type, args: [listInstance]);
    }

    public override bool Equals(object obj1, object obj2, Type type)
    {
        var items1 = (ICollection)obj1;
        var items2 = (ICollection)obj2;

        if (items1.Count != items2.Count)
        {
            return false;
        }

        var elementType = GetElementType(type);
        var enumerator1 = items1.GetEnumerator();
        var enumerator2 = items2.GetEnumerator();
        while (enumerator1.MoveNext() && enumerator2.MoveNext())
        {
            if (!ModelResolver.Equals(enumerator1.Current, enumerator2.Current, elementType))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode(object obj, Type type)
    {
        var items = (IEnumerable)obj;
        var elementType = GetElementType(type);
        HashCode hash = default;
        foreach (var item in items)
        {
            hash.Add(ModelResolver.GetHashCode(item, elementType));
        }

        return hash.ToHashCode();
    }

    private static bool IsDictionary(Type type)
    {
        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(Dictionary<,>))
            {
                return true;
            }
        }

        return false;
    }

    private static Type GetElementType(Type type)
    {
        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(Dictionary<,>))
            {
                return typeof(KeyValuePair<,>).MakeGenericType(type.GetGenericArguments());
            }
        }

        throw new UnreachableException("The type is not an ImmutableDictionary.");
    }
}
