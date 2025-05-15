using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace Libplanet.Serialization.Descriptors;

internal sealed class ImmutableSortedSetModelDescriptor : ModelDescriptorBase
{
    public override bool CanSerialize(Type type) => IsImmutableSortedSet(type);

    public override bool CanDeserialize(Type type) => IsImmutableSortedSet(type);

    public override IEnumerable<Type> GetTypes(Type type, int length, ModelOptions options)
    {
        var elementType = GetElementType(type);
        for (var i = 0; i < length; i++)
        {
            yield return elementType;
        }
    }

    public override IEnumerable<(Type Type, object? Value)> GetValues(object obj, Type type, ModelOptions options)
    {
        if (obj is IList items)
        {
            var elementType = GetElementType(type);
            for (var i = 0; i < items.Count; i++)
            {
                yield return (elementType, items[i]);
            }
        }
    }

    public override object CreateInstance(Type type, IEnumerable<object?> values, ModelOptions options)
    {
        var elementType = GetElementType(type);
        var listType = typeof(List<>).MakeGenericType(elementType);
        var listInstance = (IList)TypeUtility.CreateInstance(listType, args: [values.Count()])!;
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
