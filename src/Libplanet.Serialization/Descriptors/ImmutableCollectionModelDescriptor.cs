using System.Collections;
using System.Reflection;

namespace Libplanet.Serialization.Descriptors;

internal abstract class ImmutableCollectionModelDescriptor : CollectionModelDescriptor
{
    protected abstract Type ImmutableStaticType { get; }

    protected override IEnumerable CreateInstance(Type type, Type elementType, IList listInstance)
    {
        var methodInfo = GetCreateRangeMethod(ImmutableStaticType, typeof(IEnumerable<>));
        var genericMethodInfo = IsDictionary
            ? methodInfo.MakeGenericMethod(elementType.GetGenericArguments())
            : methodInfo.MakeGenericMethod(elementType);
        var methodArgs = new object?[] { listInstance };
        return (IEnumerable)genericMethodInfo.Invoke(null, parameters: methodArgs)!;
    }

    private static MethodInfo GetCreateRangeMethod(Type type, Type parameterType)
    {
        var bindingFlags = BindingFlags.Public | BindingFlags.Static;
        var methodInfos = type.GetMethods(bindingFlags);

        for (var i = 0; i < methodInfos.Length; i++)
        {
            var methodInfo = methodInfos[i];
            if (IsCreateRangeMethod(methodInfo, parameterType))
            {
                return methodInfo;
            }
        }

        throw new NotSupportedException("The method is not found.");
    }

    private static bool IsCreateRangeMethod(MethodInfo methodInfo, Type parameterType)
    {
        var parameters = methodInfo.GetParameters();
        if (methodInfo.Name is not "CreateRange")
        {
            return false;
        }

        if (parameters.Length is not 1)
        {
            return false;
        }

        if (parameters[0].ParameterType.Name != parameterType.Name)
        {
            return false;
        }

        return true;
    }
}
