using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Libplanet.Serialization;

public static class TypeUtility
{
    private static readonly ConcurrentDictionary<string, Type> _typeByFullName = [];
    private static readonly KnownTypes _knownTypes = new();
    private static readonly ConcurrentDictionary<Type, KnownTypes> _knownTypesByType = [];
    private static readonly ConcurrentDictionary<Type, object> _defaultByType = [];

    static TypeUtility()
    {
        _knownTypes.AddType(typeof(object), "o");
        _knownTypes.AddType(typeof(BigInteger), "bi");
        _knownTypes.AddType(typeof(bool), "b");
        _knownTypes.AddType(typeof(byte), "y");
        _knownTypes.AddType(typeof(char), "c");
        _knownTypes.AddType(typeof(DateTimeOffset), "dt");
        _knownTypes.AddType(typeof(Guid), "id");
        _knownTypes.AddType(typeof(int), "i");
        _knownTypes.AddType(typeof(long), "l");
        _knownTypes.AddType(typeof(string), "s");
        _knownTypes.AddType(typeof(TimeSpan), "ts");
        _knownTypes.AddType(typeof(Array), "ar");
        _knownTypes.AddType(typeof(List<>), "li<>");
        _knownTypes.AddType(typeof(Dictionary<,>), "di<>");
        _knownTypes.AddType(typeof(ImmutableArray<>), "imar<>");
        _knownTypes.AddType(typeof(ImmutableList<>), "imli<>");
        _knownTypes.AddType(typeof(ImmutableSortedSet<>), "imss<>");
        _knownTypes.AddType(typeof(ImmutableDictionary<,>), "imdi<,>");
        _knownTypes.AddType(typeof(ImmutableSortedDictionary<,>), "imsd<,>");

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            AddAssembly(assembly);
        }

        AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
        {
            if (args.LoadedAssembly is not null)
            {
                AddAssembly(args.LoadedAssembly);
            }
        };
    }

    public static Type GetType(string typeName)
    {
        var match = Regex.Match(
            typeName, @"^(?<type>[a-zA-Z][a-zA-Z0-9_]*)(?:(?<array>\[,*\])|(?<generic>\<.+\>))?(?<nullable>\??)$");
        if (!match.Success)
        {
            throw new ArgumentException($"Invalid type name: {typeName}", nameof(typeName));
        }

        var isNullable = match.Groups["nullable"].Value == "?";
        var name = match.Groups["type"].Value;
        var genericPart = match.Groups["generic"].Value.TrimStart('<').TrimEnd('>');
        var arrayPart = match.Groups["array"].Value;

        if (genericPart != string.Empty)
        {
            var genericArgumentNames = genericPart.Split(',');
            var genericArgumentList = new List<Type>(genericArgumentNames.Length);
            var separators = string.Empty.PadRight(genericArgumentNames.Length - 1, ',');
            var typeDefinitionName = $"{name}<{separators}>";
            var typeDefinition = _knownTypes.GetType(typeDefinitionName);
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
        else if (arrayPart != string.Empty)
        {
            var elementTypeName = name;
            var elementType = _knownTypes.GetType(elementTypeName);
            if (isNullable)
            {
                elementType = typeof(Nullable<>).MakeGenericType(elementType);
            }

            var rank = arrayPart.Count(c => c == ',') + 1;
            return Array.CreateInstance(elementType, new int[rank]).GetType();
        }
        else
        {
            var type = _knownTypes.GetType(name);
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
            var typeDefinitionName = _knownTypes.GetTypeName(typeDefinition);
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
        else if (type.IsArray)
        {
            var elementType = type.GetElementType()
                ?? throw new UnreachableException("Array type does not have an element type");
            var elementTypeName = GetTypeName(elementType);
            var rank = type.GetArrayRank();
            return $"{elementTypeName}[{new string(',', rank - 1)}]";
        }
        else if (_knownTypes.TryGetTypeName(type, out var typeName))
        {
            return typeName;
        }
        else if (type.IsDefined(typeof(OriginModelAttribute)))
        {
            var attribute = type.GetCustomAttribute<OriginModelAttribute>()!;
            return GetTypeName(attribute.Type);
        }

        throw new NotSupportedException(
            $"Type '{type.FullName}' is not supported or not registered in known types.");
    }

    public static bool IsDefault(object value, Type type)
    {
        if (type.IsValueType)
        {
            var defaultValue = _defaultByType.GetOrAdd(type, CreateDefault);
            return ReferenceEquals(value, defaultValue) || Equals(value, defaultValue);
        }

        return false;
    }

    public static object GetDefault(Type type)
    {
        if (type.IsValueType)
        {
            return _defaultByType.GetOrAdd(type, CreateDefault);
        }

        throw new ArgumentException(
            $"Type '{type.FullName}' is not a value type and does not have a default value.",
            nameof(type));
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

    private static void AddAssembly(Assembly assembly)
    {
        var query = from type in assembly.GetTypes()
                    where type.IsDefined(typeof(ModelAttribute)) ||
                          type.IsDefined(typeof(ModelConverterAttribute))
                    select type;

        foreach (var item in query)
        {
            if (item.IsDefined(typeof(ModelAttribute)))
            {
                var attribute = item.GetCustomAttribute<ModelAttribute>()
                    ?? throw new UnreachableException("ModelAttribute cannot be null");
                _knownTypes.AddType(item, attribute.TypeName);
            }
            else if (item.IsDefined(typeof(ModelConverterAttribute)))
            {
                var attribute = item.GetCustomAttribute<ModelConverterAttribute>()
                    ?? throw new UnreachableException("ModelConverterAttribute cannot be null");
                _knownTypes.AddType(item, attribute.TypeName);
            }

            if (item.IsDefined(typeof(ModelKnownTypeAttribute)))
            {
                var knownTypeAttributes = item.GetCustomAttributes<ModelKnownTypeAttribute>();
                if (!_knownTypesByType.TryGetValue(item, out var typeResolver))
                {
                    typeResolver = new KnownTypes();
                    _knownTypesByType[item] = typeResolver;
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
        if (_knownTypesByType.TryGetValue(typeDefinition, out var typeResolver)
            && typeResolver.TryGetTypeName(genericArgument, out var genericArgumentName))
        {
            return genericArgumentName;
        }

        return GetTypeName(genericArgument);
    }

    private static Type GetGenericArgument(Type typeDefinition, string genericArgumentName)
    {
        if (_knownTypesByType.TryGetValue(typeDefinition, out var typeResolver)
            && typeResolver.TryGetType(genericArgumentName, out var genericArgumentType))
        {
            return genericArgumentType;
        }

        return GetType(genericArgumentName);
    }
}
