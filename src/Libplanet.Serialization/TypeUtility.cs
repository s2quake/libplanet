using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Bencodex.Types;

namespace Libplanet.Serialization;

public static class TypeUtility
{
    private static readonly ConcurrentDictionary<string, Type> _typeByName = [];

    static TypeUtility()
    {
        var assembly = typeof(ModelSerializer).Assembly;
        var types = GetSerializableTypes(assembly);
        foreach (var type in types)
        {
            var typeName = type.FullName
                ?? throw new UnreachableException("Type does not have FullName");
            _typeByName[typeName] = type;
        }
    }

    public static Type GetType(string typeName)
    {
        if (!_typeByName.TryGetValue(typeName, out var type))
        {
            type = Type.GetType(typeName)
                ?? throw new ArgumentException($"Type {typeName} is not found", nameof(typeName));
            _typeByName[typeName] = type;
        }

        return type;
    }

    public static bool TryGetType(string typeName, [MaybeNullWhen(false)] out Type type)
    {
        if (!_typeByName.TryGetValue(typeName, out type))
        {
            type = Type.GetType(typeName);
            if (type is not null)
            {
                _typeByName[typeName] = type;
            }
        }

        return type is not null;
    }

    public static bool IsStandardType(Type type)
    {
        if (type.IsEnum || IsBencodableType(type) || IsBencodexType(type))
        {
            return true;
        }

        return false;
    }

    public static bool IsNullableType(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);

    public static bool IsTupleType(Type type)
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

    public static bool IsBencodableType(Type type)
        => typeof(IBencodable).IsAssignableFrom(type) && !type.IsInterface;

    public static bool IsBencodexType(Type type)
        => typeof(IValue).IsAssignableFrom(type) && !type.IsInterface;

    private static IEnumerable<Type> GetSerializableTypes(Assembly assembly)
    {
        var types = assembly.GetTypes().Where(IsDefined);
        foreach (var type in types)
        {
            yield return type;
        }

        static bool IsDefined(Type type) => type.IsDefined(typeof(ModelAttribute));
    }
}
