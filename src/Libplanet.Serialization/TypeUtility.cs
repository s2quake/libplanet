using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using Bencodex;
using Bencodex.Types;

namespace Libplanet.Serialization;

public static class TypeUtility
{
    public static readonly Type[] SupportedBaseTypes =
    [
        typeof(int),
        typeof(long),
        typeof(string),
        typeof(bool),
        typeof(BigInteger),
        typeof(byte[]),
        typeof(DateTimeOffset),
        typeof(TimeSpan),
    ];

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
        if (SupportedBaseTypes.Contains(type)
            || type.IsEnum
            || IsBencodableType(type)
            || IsBencodexType(type))
        {
            return true;
        }

        return false;
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
