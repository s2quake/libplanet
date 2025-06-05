using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Libplanet.Serialization;

public static class TypeUtility
{
    private static readonly ConcurrentDictionary<string, Type> _typeByName = [];
    private static readonly ConcurrentDictionary<Type, string> _nameByType = [];
    private static readonly ConcurrentDictionary<Type, object> _defaultByType = [];

    static TypeUtility()
    {
        AddType(typeof(BigInteger), "bi");
        AddType(typeof(bool), "b");
        AddType(typeof(byte), "by");
        AddType(typeof(char), "c");
        AddType(typeof(DateTimeOffset), "dt");
        AddType(typeof(Guid), "id");
        AddType(typeof(int), "i");
        AddType(typeof(long), "l");
        AddType(typeof(string), "s");
        AddType(typeof(TimeSpan), "ts");
        AddType(typeof(Array), "ar");
        AddType(typeof(ImmutableArray<>), "imar<>");
        AddType(typeof(ImmutableList<>), "imli<>");
        AddType(typeof(ImmutableSortedSet<>), "imss<>");
        AddType(typeof(ImmutableDictionary<,>), "imdi<,>");
        AddType(typeof(ImmutableSortedDictionary<,>), "imsd<,>");

        InitializeModelTypes();
    }

    private static void AddType(Type type, string typeName)
    {
        if (_typeByName.ContainsKey(typeName))
        {
            throw new ArgumentException($"Type name '{typeName}' is already registered.", nameof(typeName));
        }

        if (_nameByType.ContainsKey(type))
        {
            throw new ArgumentException($"Type '{type.FullName}' is already registered.", nameof(type));
        }

        _typeByName.TryAdd(typeName, type);
        _nameByType.TryAdd(type, typeName);
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

    public static bool IsNullableType(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);

    public static string GetTypeName(Type type)
    {
        var name = GetName(type);
        var ns = type.Namespace ?? throw new UnreachableException("Type does not have FullName");
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
        if (type.Name is null)
        {
            throw new UnreachableException("Type does not have FullName");
        }

        if (_nameByType.TryGetValue(type, out var name))
        {
            if (type.IsGenericType)
            {
                var s = $"<{string.Join(',', type.GetGenericArguments().Length - 1)}>";
                // If the type is already registered, we can return its name directly.
                // This avoids unnecessary recomputation of the generic arguments.
                return name;
            }

            return name;
        }

        name = type.Name;
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
            return type.Name;
        }

        return $"{GetName(type.DeclaringType)}+{name}";
    }

    private static void InitializeModelTypes()
    {
        var query = from assembly in AppDomain.CurrentDomain.GetAssemblies()
                    from type in assembly.GetTypes()
                    where type.IsDefined(typeof(ModelAttribute)) ||
                          type.IsDefined(typeof(ModelConverterAttribute))
                    select type;

        foreach (var item in query)
        {
            if (item.IsDefined(typeof(ModelAttribute)))
            {
                var attribute = item.GetCustomAttribute<ModelAttribute>()
                    ?? throw new UnreachableException("ModelAttribute cannot be null");
                AddType(item, attribute.TypeName);
            }
            else if (item.IsDefined(typeof(ModelConverterAttribute)))
            {
                var attribute = item.GetCustomAttribute<ModelConverterAttribute>()
                    ?? throw new UnreachableException("ModelConverterAttribute cannot be null");
                AddType(item, attribute.TypeName);
            }
        }
    }

    private static object CreateDefault(Type type)
        => Activator.CreateInstance(type) ?? throw new UnreachableException("ValueType cannot be null");
}
