using System.Collections;

namespace Libplanet.Serialization;

public static class ModelUtility
{
    private static readonly IModelResolver _resolver = ModelResolver.Default;

    public static int GetHashCode(object obj)
    {
        var properties = _resolver.GetProperties(obj.GetType());
        HashCode hash = default;
        foreach (var property in properties)
        {
            var value = property.GetValue(obj);
            if (value is IList list)
            {
                AddArray(hash, list);
            }
            else
            {
                hash.Add(value);
            }
        }

        return hash.ToHashCode();
    }

    public static bool Equals<T>(T left, T? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (right is null)
        {
            return false;
        }

        var properties = _resolver.GetProperties(typeof(T));
        foreach (var property in properties)
        {
            var leftValue = property.GetValue(left);
            var rightValue = property.GetValue(right);
            if (ArrayUtility.IsSupportedArrayType(property.PropertyType, out var elementType))
            {
                if (!Equals(leftValue as IList, rightValue as IList, elementType))
                {
                    return false;
                }
            }
            else if (!object.Equals(leftValue, rightValue))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Equals(IList? left, IList? right, Type elementType)
    {
        if (left is null || right is null)
        {
            return false;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        if (ArrayUtility.IsSupportedArrayType(elementType, out var elementType1))
        {
            for (var i = 0; i < left.Count; i++)
            {
                if (!Equals(left[i] as IList, right[i] as IList, elementType1))
                {
                    return false;
                }
            }
        }
        else
        {
            for (var i = 0; i < left.Count; i++)
            {
                if (!object.Equals(left[i], right[i]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void AddArray(HashCode hash, IList list)
    {
        foreach (var item in list)
        {
            if (item is IList nestedList)
            {
                AddArray(hash, nestedList);
            }
            else
            {
                hash.Add(item);
            }
        }
    }
}
