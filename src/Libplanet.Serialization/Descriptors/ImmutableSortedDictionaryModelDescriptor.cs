using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace Libplanet.Serialization.Descriptors;

internal sealed class ImmutableSortedDictionaryModelDescriptor : ModelDescriptorBase
{
    public override bool CanSerialize(Type type) => IsImmutableSortedDictionary(type);

    public override bool CanDeserialize(Type type) => IsImmutableSortedDictionary(type);

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
        if (obj is IEnumerable items)
        {
            var elementType = GetElementType(type);
            foreach (var item in items)
            {
                yield return (elementType, item);
            }
        }
    }

    public override object CreateInstance(Type type, IEnumerable<object?> values, ModelOptions options)
    {
        var elementType = GetElementType(type);
        var listType = typeof(List<>).MakeGenericType(elementType);
        var listInstance = (IList)TypeUtility.CreateInstance(listType, args: [values.Count()])!;
        var methodName = nameof(ImmutableSortedDictionary.CreateRange);
        var methodInfo = GetCreateRangeMethod(
            typeof(ImmutableSortedDictionary), methodName, typeof(IEnumerable<>));
        var genericMethodInfo = methodInfo.MakeGenericMethod(elementType.GetGenericArguments());
        foreach (var value in values)
        {
            listInstance.Add(value);
        }

        var methodArgs = new object?[] { listInstance };
        return genericMethodInfo.Invoke(null, parameters: methodArgs)!;
    }

    private static bool IsImmutableSortedDictionary(Type type)
    {
        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(ImmutableSortedDictionary<,>))
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
            if (genericTypeDefinition == typeof(ImmutableSortedDictionary<,>))
            {
                return typeof(KeyValuePair<,>).MakeGenericType(type.GetGenericArguments());
            }
        }

        throw new UnreachableException("The type is not an ImmutableSortedDictionary.");
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
