using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace Libplanet.Serialization.Descriptors;

internal sealed class ImmutableSortedSetModelDescriptor : ModelDescriptor
{
    public override bool CanSerialize(Type type) => IsImmutableSortedSet(type);

    public override Type[] GetTypes(Type type, out bool isArray)
    {
        isArray = true;
        return [GetElementType(type)];
    }

    public override object?[] Serialize(object obj, Type type, ModelOptions options)
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

    public override object Deserialize(Type type, object?[] values, ModelOptions options)
    {
        var elementType = GetElementType(type);
        var listType = typeof(List<>).MakeGenericType(elementType);
        var listInstance = (IList)TypeUtility.CreateInstance(listType, args: [values.Length])!;
        var methodName = nameof(ImmutableSortedSet.CreateRange);
        var methodInfo = GetCreateRangeMethod(
            typeof(ImmutableSortedSet), methodName, typeof(IEnumerable<>));
        var genericMethodInfo = methodInfo.MakeGenericMethod(elementType);
        foreach (var value in values)
        {
            listInstance.Add(value);
        }

        var methodArgs = new object?[] { listInstance };
        return genericMethodInfo.Invoke(null, parameters: methodArgs)!;
    }

    public override bool Equals(object obj1, object obj2, Type type)
    {
        var items1 = (IList)obj1;
        var items2 = (IList)obj2;
        if (items1.Count != items2.Count)
        {
            return false;
        }

        var elementType = GetElementType(type);
        for (var i = 0; i < items1.Count; i++)
        {
            var item1 = items1[i];
            var item2 = items2[i];
            if (!ModelResolver.Equals(item1, item2, elementType))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode(object obj, Type type)
    {
        var items = (IList)obj;
        var elementType = GetElementType(type);
        HashCode hash = default;
        foreach (var item in items)
        {
            hash.Add(ModelResolver.GetHashCode(item, elementType));
        }

        return hash.ToHashCode();
    }

    private static bool IsImmutableSortedSet(Type type)
    {
        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(ImmutableSortedSet<>))
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
            if (genericTypeDefinition == typeof(ImmutableSortedSet<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        throw new UnreachableException("The type is not an ImmutableSortedSet.");
    }

    private static MethodInfo GetCreateRangeMethod(Type type, string methodName, Type parameterType)
    {
        var parameterName = parameterType.Name;
        var bindingFlags = BindingFlags.Public | BindingFlags.Static;
        var methodInfos = type.GetMethods(bindingFlags);

        for (var i = 0; i < methodInfos.Length; i++)
        {
            var methodInfo = methodInfos[i];
            var parameters = methodInfo.GetParameters();
            if (methodInfo.Name == methodName &&
                parameters.Length == 1 &&
                parameters[0].ParameterType.Name == parameterName)
            {
                return methodInfo;
            }
        }

        throw new NotSupportedException("The method is not found.");
    }
}
