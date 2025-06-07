using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Libplanet.Serialization;

public static class TypeUtility
{
    private static readonly ConcurrentDictionary<string, Type> _typeByFullName = [];
    private static readonly ConcurrentDictionary<string, Type> _typeByName = [];
    private static readonly ConcurrentDictionary<Type, string> _nameByType = [];
    private static readonly ConcurrentDictionary<Type, object> _defaultByType = [];

    static TypeUtility()
    {
        AddType(typeof(object), "o");
        AddType(typeof(BigInteger), "bi");
        AddType(typeof(bool), "b");
        AddType(typeof(byte), "y");
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

        if (type.IsGenericType)
        {
            int wqer = 0;
        }

        _typeByName.TryAdd(typeName, type);
        _nameByType.TryAdd(type, typeName);
    }

    public static Type GetType(string typeName)
    {
        var match = Regex.Match(typeName, @"^(?<type>[a-zA-Z][a-zA-Z0-9_]*)(?<generic>\<.+\>)?(?<nullable>\??)$");
        var isNullable = match.Groups["nullable"].Value == "?";
        var name = match.Groups["type"].Value;
        var genericPart = match.Groups["generic"].Value.TrimStart('<').TrimEnd('>');

        if (genericPart != string.Empty)
        {
            var genericTypeNames = genericPart.Split(',');
            var genericTypeList = new List<Type>(genericTypeNames.Length);
            var separators = string.Empty.PadRight(genericTypeNames.Length - 1, ',');
            var typeDefinitionName = $"{name}<{separators}>";
            var typeDefinition = _typeByName[typeDefinitionName];
            foreach (var genericTypeName in genericTypeNames)
            {
                genericTypeList.Add(GetType(genericTypeName));
            }

            var type = typeDefinition.MakeGenericType([.. genericTypeList]);
            if (isNullable)
            {
                return typeof(Nullable<>).MakeGenericType(type);
            }

            return type;
        }
        else
        {
            var type = _typeByName[name];
            if (isNullable)
            {
                return typeof(Nullable<>).MakeGenericType(type);
            }

            return type;
        }
    }

    public static bool TryGetType(string typeName, [MaybeNullWhen(false)] out Type type)
    {
        if (!_typeByFullName.TryGetValue(typeName, out type))
        {
            type = Type.GetType(typeName);
            if (type is not null)
            {
                _typeByFullName[typeName] = type;
            }
        }

        return type is not null;
    }

    public static bool IsNullableType(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);

    public static string GetTypeName(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlyingType)
        {
            return $"{GetTypeName(underlyingType)}?";
        }
        else if (type.IsGenericType)
        {
            var typeDefinition = type.GetGenericTypeDefinition();
            var typeDefinitionName = _nameByType[typeDefinition];
            var genericArguments = type.GetGenericArguments();
            var genericArgumentList = new List<string>(genericArguments.Length);
            foreach (var genericArgument in genericArguments)
            {
                var genericArgumentName = GetTypeName(genericArgument);
                genericArgumentList.Add(genericArgumentName);
            }
            var genericArgumentString = string.Join(',', genericArgumentList);
            return Regex.Replace(typeDefinitionName, "<.*>", $"<{genericArgumentString}>");
        }

        return _nameByType[type];
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

    public static string GetFullName(Type type)
    {
        if (type.Name is null)
        {
            throw new UnreachableException("Type does not have FullName");
        }

        var name = type.Name;
        if (type.IsGenericType)
        {
            var genericArguments = type.GetGenericArguments();
            var nameList = new List<string>(genericArguments.Length);
            foreach (var genericArgument in genericArguments)
            {
                var genericArgumentName = $"[{GetFullName(genericArgument)}]";
                nameList.Add(genericArgumentName);
            }

            name = $"{name}[{string.Join(',', nameList)}]";
        }

        if (type.DeclaringType is null)
        {
            return type.Name;
        }

        return $"{GetFullName(type.DeclaringType)}+{name}";
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

            if (item.IsDefined(typeof(ModelKnownTypeAttribute)))
            {
                var knownTypeAttributes = item.GetCustomAttributes<ModelKnownTypeAttribute>();
                foreach (var knownTypeAttribute in knownTypeAttributes)
                {
                    AddType(knownTypeAttribute.Type, knownTypeAttribute.TypeName);
                }
            }
        }
    }

    private static object CreateDefault(Type type)
        => Activator.CreateInstance(type) ?? throw new UnreachableException("ValueType cannot be null");
}
