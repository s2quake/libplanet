using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Bencodex.Types;

namespace Libplanet.Serialization;

public static class TypeUtility
{
    private static readonly ConcurrentDictionary<string, Type> _typeByName = [];
    private static readonly ConcurrentDictionary<Type, object> _defaultByType = [];

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

    public static bool IsBencodableType(Type type) => typeof(IBencodable).IsAssignableFrom(type);

    public static bool IsBencodexType(Type type) => typeof(IValue).IsAssignableFrom(type);

    public static string GetTypeName(Type type)
    {
        var name = GetName(type);
        var ns = type.Namespace
            ?? throw new UnreachableException("Type does not have FullName");
        var assemblyName = type.Assembly.GetName().Name
             ?? throw new UnreachableException("Assembly does not have Name");

        return $"{ns}.{name}, {assemblyName}";
    }

    public static bool IsDefault(object? value, Type type)
    {
        if (type.IsValueType)
        {
            if (value is null)
            {
                throw new UnreachableException("ValueType cannot be null");
            }

            var defaultValue = _defaultByType.GetOrAdd(type, CreateDefault);
            return ReferenceEquals(value, defaultValue);
        }

        return value is null;

    }

    public static object? GetDefault(Type type)
    {
        if (type.IsValueType)
        {
            return _defaultByType.GetOrAdd(type, CreateDefault);
        }

        return null;
    }

    public static object CreateInstance(Type type, params object?[] args)
    {
        try
        {
            if (Activator.CreateInstance(type, args: args) is { } obj)
            {
                return obj;
            }
        }
        catch (Exception e)
        {
            throw new ModelCreationException(type, e);
        }

        throw new ModelCreationException(type);
    }

    private static string GetName(Type type)
    {
        var name = type.Name ?? throw new UnreachableException("Type does not have FullName");

        if (type.IsGenericType)
        {
            var genericArguments = type.GetGenericArguments();
            var nameList = new List<string>(genericArguments.Length);
            foreach (var genericArgument in genericArguments)
            {
                var genericArgumentName = $"[{GetTypeName(genericArgument)}]";
                nameList.Add(genericArgumentName);
            }

            name = $"{name}[{string.Join(',', nameList)}]";
        }

        if (type.DeclaringType is null)
        {
            return name;
        }

        return $"{GetName(type.DeclaringType)}+{name}";
    }

    private static IEnumerable<Type> GetSerializableTypes(Assembly assembly)
    {
        var types = assembly.GetTypes().Where(IsDefined);
        foreach (var type in types)
        {
            yield return type;
        }

        static bool IsDefined(Type type) => type.IsDefined(typeof(ModelAttribute));
    }

    private static object CreateDefault(Type type)
        => Activator.CreateInstance(type) ?? throw new UnreachableException("ValueType cannot be null");
}
