using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Libplanet.Serialization;

public static class TypeUtility
{
    private static readonly ConcurrentDictionary<string, Type> _typeByFullName = [];
    private static readonly TypeResolver _typeResolver = new();
    private static readonly ConcurrentDictionary<Type, TypeResolver> _knownTypeResolvers = [];
    private static readonly ConcurrentDictionary<Type, object> _defaultByType = [];

    static TypeUtility()
    {
        _typeResolver.AddType(typeof(object), "o");
        _typeResolver.AddType(typeof(BigInteger), "bi");
        _typeResolver.AddType(typeof(bool), "b");
        _typeResolver.AddType(typeof(byte), "y");
        _typeResolver.AddType(typeof(char), "c");
        _typeResolver.AddType(typeof(DateTimeOffset), "dt");
        _typeResolver.AddType(typeof(Guid), "id");
        _typeResolver.AddType(typeof(int), "i");
        _typeResolver.AddType(typeof(long), "l");
        _typeResolver.AddType(typeof(string), "s");
        _typeResolver.AddType(typeof(TimeSpan), "ts");
        _typeResolver.AddType(typeof(Array), "ar");
        _typeResolver.AddType(typeof(ImmutableArray<>), "imar<>");
        _typeResolver.AddType(typeof(ImmutableList<>), "imli<>");
        _typeResolver.AddType(typeof(ImmutableSortedSet<>), "imss<>");
        _typeResolver.AddType(typeof(ImmutableDictionary<,>), "imdi<,>");
        _typeResolver.AddType(typeof(ImmutableSortedDictionary<,>), "imsd<,>");

        InitializeModelTypes();
    }

    public static Type GetType(string typeName)
    {
        var match = Regex.Match(typeName, @"^(?<type>[a-zA-Z][a-zA-Z0-9_]*)(?<generic>\<.+\>)?(?<nullable>\??)$");
        var isNullable = match.Groups["nullable"].Value == "?";
        var name = match.Groups["type"].Value;
        var genericPart = match.Groups["generic"].Value.TrimStart('<').TrimEnd('>');

        if (genericPart != string.Empty)
        {
            var genericArgumentNames = genericPart.Split(',');
            var genericArgumentList = new List<Type>(genericArgumentNames.Length);
            var separators = string.Empty.PadRight(genericArgumentNames.Length - 1, ',');
            var typeDefinitionName = $"{name}<{separators}>";
            var typeDefinition = _typeResolver.GetType(typeDefinitionName);
            foreach (var genericArgumentName in genericArgumentNames)
            {
                var genericArgument = GetGenericArgument(typeDefinition, genericArgumentName);
                genericArgumentList.Add(genericArgument);
            }

            var type = typeDefinition.MakeGenericType([.. genericArgumentList]);
            if (isNullable)
            {
                return typeof(Nullable<>).MakeGenericType(type);
            }

            return type;
        }
        else
        {
            var type = _typeResolver.GetType(name);
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
            var typeDefinitionName = _typeResolver.GetTypeName(typeDefinition);
            var genericArguments = type.GetGenericArguments();
            var genericArgumentList = new List<string>(genericArguments.Length);
            foreach (var genericArgument in genericArguments)
            {
                var genericArgumentName = GetGenericArgumentName(typeDefinition, genericArgument);
                genericArgumentList.Add(genericArgumentName);
            }
            var genericArgumentString = string.Join(',', genericArgumentList);
            return Regex.Replace(typeDefinitionName, "<.*>", $"<{genericArgumentString}>");
        }

        return _typeResolver.GetTypeName(type);
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
                _typeResolver.AddType(item, attribute.TypeName);
            }
            else if (item.IsDefined(typeof(ModelConverterAttribute)))
            {
                var attribute = item.GetCustomAttribute<ModelConverterAttribute>()
                    ?? throw new UnreachableException("ModelConverterAttribute cannot be null");
                _typeResolver.AddType(item, attribute.TypeName);
            }

            if (item.IsDefined(typeof(ModelKnownTypeAttribute)))
            {
                var knownTypeAttributes = item.GetCustomAttributes<ModelKnownTypeAttribute>();
                if (!_knownTypeResolvers.TryGetValue(item, out var typeResolver))
                {
                    typeResolver = new TypeResolver();
                    _knownTypeResolvers[item] = typeResolver;
                }

                foreach (var knownTypeAttribute in knownTypeAttributes)
                {
                    typeResolver.AddType(knownTypeAttribute.Type, knownTypeAttribute.TypeName);
                }
            }
        }
    }

    private static object CreateDefault(Type type)
        => Activator.CreateInstance(type) ?? throw new UnreachableException("ValueType cannot be null");

    private static string GetGenericArgumentName(Type typeDefinition, Type genericArgument)
    {
        if (_knownTypeResolvers.TryGetValue(typeDefinition, out var typeResolver)
            && typeResolver.TryGetTypeName(genericArgument, out var genericArgumentName))
        {
            return genericArgumentName;
        }

        return GetTypeName(genericArgument);
    }

    private static Type GetGenericArgument(Type typeDefinition, string genericArgumentName)
    {
        if (_knownTypeResolvers.TryGetValue(typeDefinition, out var typeResolver)
            && typeResolver.TryGetType(genericArgumentName, out var genericArgumentType))
        {
            return genericArgumentType;
        }

        return GetType(genericArgumentName);
    }
}
