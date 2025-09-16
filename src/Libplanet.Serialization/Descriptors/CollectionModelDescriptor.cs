using System.Collections;

namespace Libplanet.Serialization.Descriptors;

internal abstract class CollectionModelDescriptor : ModelDescriptor
{
    protected abstract Type GenericTypeDefinition { get; }

    protected abstract bool IsDictionary { get; }

    public override bool CanSerialize(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == GenericTypeDefinition;

    protected virtual IEnumerable CreateInstance(Type type, Type elementType, IList listInstance)
        => (IEnumerable)TypeUtility.CreateInstance(type, args: [listInstance]);

    protected virtual Type GetElementType(Type type)
    {
        if (CanSerialize(type))
        {
            if (IsDictionary)
            {
                return typeof(KeyValuePair<,>).MakeGenericType(type.GetGenericArguments());
            }

            return type.GetGenericArguments()[0];
        }

        throw new NotSupportedException("The type is not supported.");
    }

    private static IList CreateListInstance(Type elementType)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        return (IList)TypeUtility.CreateInstance(listType, args: [])!;
    }

    public sealed override Type[] GetTypes(Type type, out bool isArray)
    {
        isArray = true;
        return [GetElementType(type)];
    }

    public override object?[] Serialize(object obj, Type type, ModelOptions options)
    {
        if (obj is ICollection collection)
        {
            var values = new object?[collection.Count];
            var i = 0;
            var enumerator = collection.GetEnumerator();
            while (enumerator.MoveNext())
            {
                values[i++] = enumerator.Current;
            }

            return values;
        }
        else if (obj is IEnumerable enumerable)
        {
            var valueList = new List<object?>();
            foreach (var item in enumerable)
            {
                valueList.Add(item);
            }

            return [.. valueList];
        }
        else
        {
            throw new InvalidOperationException($"Cannot get values from {obj.GetType()}");
        }
    }

    public override object Deserialize(Type type, object?[] values, ModelOptions options)
    {
        var elementType = GetElementType(type);
        var listInstance = CreateListInstance(elementType);
        foreach (var value in values)
        {
            listInstance.Add(value);
        }

        return CreateInstance(type, elementType, listInstance);
    }

    public override bool Equals(object obj1, object obj2, Type type)
    {
        if (obj1.GetType() != obj2.GetType())
        {
            return false;
        }

        if (type.IsValueType)
        {
            if (TypeUtility.IsDefault(obj1, type) && TypeUtility.IsDefault(obj2, type))
            {
                return true;
            }

            if (TypeUtility.IsDefault(obj1, type) || TypeUtility.IsDefault(obj2, type))
            {
                return false;
            }
        }

        if (obj1 is ICollection collection1
            && obj2 is ICollection collection2
            && collection1.Count != collection2.Count)
        {
            return false;
        }

        var items1 = (IEnumerable)obj1;
        var items2 = (IEnumerable)obj2;

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
        if (type.IsValueType && TypeUtility.IsDefault(obj, type))
        {
            return 0;
        }

        var items = (IEnumerable)obj;
        var elementType = GetElementType(type);
        HashCode hash = default;
        foreach (var item in items)
        {
            hash.Add(ModelResolver.GetHashCode(item, elementType));
        }

        return hash.ToHashCode();
    }
}
