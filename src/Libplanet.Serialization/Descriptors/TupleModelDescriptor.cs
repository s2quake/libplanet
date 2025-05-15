using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Libplanet.Serialization.Descriptors;

internal sealed class TupleModelDescriptor : ModelDescriptorBase
{
    public override bool CanSerialize(Type type) => IsTuple(type) || IsValueTupleType(type);

    public override bool CanDeserialize(Type type) => IsTuple(type) || IsValueTupleType(type);

    public override IEnumerable<Type> GetTypes(Type type, int length, ModelOptions options)
    {
        var genericArguments = type.GetGenericArguments();
        if (genericArguments.Length != length)
        {
            throw new ModelSerializationException(
                $"The number of generic arguments {genericArguments.Length} does not match " +
                $"the number of tuple items {length}");
        }

        for (var i = 0; i < genericArguments.Length; i++)
        {
            yield return genericArguments[i];
        }
    }

    public override IEnumerable<(Type Type, object? Value)> GetValues(object obj, Type type, ModelOptions options)
    {
        var genericArguments = type.GetGenericArguments();
        if (obj is not ITuple tuple)
        {
            throw new ModelSerializationException(
                $"The value {obj} is not a tuple of type {type}");
        }

        if (genericArguments.Length != tuple.Length)
        {
            throw new ModelSerializationException(
                $"The number of generic arguments {genericArguments.Length} does not match " +
                $"the number of tuple items {tuple.Length}");
        }

        for (var i = 0; i < genericArguments.Length; i++)
        {
            var item = tuple[i];
            var itemType = genericArguments[i];
            yield return (itemType, item);
        }
    }

    public override object CreateInstance(Type type, IEnumerable<object?> values, ModelOptions options)
        => TypeUtility.CreateInstance(type, args: [.. values]);

    private static bool IsTuple(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var genericTypeDefinition = type.GetGenericTypeDefinition();
        return genericTypeDefinition == typeof(Tuple<,>)
            || genericTypeDefinition == typeof(Tuple<,,>)
            || genericTypeDefinition == typeof(Tuple<,,,>)
            || genericTypeDefinition == typeof(Tuple<,,,,>)
            || genericTypeDefinition == typeof(Tuple<,,,,,>)
            || genericTypeDefinition == typeof(Tuple<,,,,,,>)
            || genericTypeDefinition == typeof(Tuple<,,,,,,,>);
    }

    public static bool IsValueTupleType(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var genericTypeDefinition = type.GetGenericTypeDefinition();
        return genericTypeDefinition == typeof(ValueTuple<>)
            || genericTypeDefinition == typeof(ValueTuple<,>)
            || genericTypeDefinition == typeof(ValueTuple<,,>)
            || genericTypeDefinition == typeof(ValueTuple<,,,>)
            || genericTypeDefinition == typeof(ValueTuple<,,,,>)
            || genericTypeDefinition == typeof(ValueTuple<,,,,,>)
            || genericTypeDefinition == typeof(ValueTuple<,,,,,,>)
            || genericTypeDefinition == typeof(ValueTuple<,,,,,,,>);
    }

    private static Type GetElementType(Type type)
    {
        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(ImmutableArray<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        throw new UnreachableException("The type is not an ImmutableArray.");
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
