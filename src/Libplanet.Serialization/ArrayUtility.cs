using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Libplanet.Serialization;

public static class ArrayUtility
{
    private static readonly ConcurrentDictionary<Type, Array> _emptyArrayByElementType = [];
    private static readonly ConcurrentDictionary<Type, object> _immutableEmptyArrayByElementType
        = [];

    private static readonly ConcurrentDictionary<Type, Type> _arrayTypeByElementType = [];
    private static readonly ConcurrentDictionary<Type, Type> _immutableArrayTypeElementType = [];

    public static bool IsStandardArrayType(Type type) => IsSupportedArrayType(type, out _);

    public static bool IsSupportedArrayType(Type type, [MaybeNullWhen(false)] out Type elementType)
    {
        if (IsArray(type, out elementType) == true)
        {
            return true;
        }

        if (IsImmutableArray(type, out elementType) == true)
        {
            return true;
        }

        return false;
    }

    public static bool IsArray(Type type) => IsArray(type, out _);

    public static bool IsArray(Type type, [MaybeNullWhen(false)] out Type elementType)
    {
        if (typeof(Array).IsAssignableFrom(type) == true)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        elementType = null;
        return false;
    }

    public static bool IsImmutableArray(Type type) => IsImmutableArray(type, out _);

    public static bool IsImmutableArray(Type type, [MaybeNullWhen(false)] out Type elementType)
    {
        if (type.IsGenericType == true)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(ImmutableArray<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null;
        return false;
    }

    public static Array ToEmptyArray(Type elementType)
        => _emptyArrayByElementType.GetOrAdd(elementType, CreateEmptyArray);

    public static object ToImmutableEmptyArray(Type elementType)
        => _immutableEmptyArrayByElementType.GetOrAdd(elementType, CreateImmutableEmptyArray);

    public static Type GetArrayType(Type elementType)
        => _arrayTypeByElementType.GetOrAdd(elementType, item => item.MakeArrayType());

    public static Type GetImmutableArrayType(Type elementType)
        => _immutableArrayTypeElementType.GetOrAdd(
            elementType, item => typeof(ImmutableArray<>).MakeGenericType(item));

    private static object CreateImmutableEmptyArray(Type elementType)
    {
        var bindingFlags = BindingFlags.Public | BindingFlags.Static;
        var arrayType = typeof(ImmutableArray<>).MakeGenericType(elementType);
        var propertyInfo = arrayType.GetField(nameof(ImmutableArray<object>.Empty), bindingFlags)
            ?? throw new UnreachableException("Field is not found.");
        return propertyInfo.GetValue(null)
            ?? throw new UnreachableException("Field value is null.");
    }

    private static Array CreateEmptyArray(Type elementType)
    {
        var methodName = nameof(Array.Empty);
        var method = typeof(Array).GetMethod(methodName)
            ?? throw new NotSupportedException("The method is not found.");
        var genericMethod = method.MakeGenericMethod(elementType);
        return (Array)genericMethod.Invoke(null, parameters: null)!;
    }
}
