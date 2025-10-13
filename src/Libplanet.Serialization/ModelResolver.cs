using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Libplanet.Serialization.DynamicConverters;
using Libplanet.Serialization.StaticConverters;

namespace Libplanet.Serialization;

public static class ModelResolver
{
    private static readonly object _lock = new();
    private static readonly ConcurrentDictionary<Type, ModelPropertyCollection> _declaredPropertiesByType = [];
    private static readonly ConcurrentDictionary<Type, ModelPropertyCollection> _propertiesByType = [];
    private static readonly ConcurrentDictionary<Type, ImmutableArray<Type>> _typesByType = [];
    private static readonly ConcurrentDictionary<Type, IModelConverter> _converterByType = new()
    {
        [typeof(BigInteger)] = new BigIntegerModelConverter(),
        [typeof(bool)] = new BooleanModelConverter(),
        [typeof(byte)] = new ByteModelConverter(),
        [typeof(char)] = new CharModelConverter(),
        [typeof(DateTimeOffset)] = new DateTimeOffsetModelConverter(),
        [typeof(Guid)] = new GuidModelConverter(),
        [typeof(int)] = new Int32ModelConverter(),
        [typeof(long)] = new Int64ModelConverter(),
        [typeof(string)] = new StringModelConverter(),
        [typeof(TimeSpan)] = new TimeSpanModelConverter(),
    };
    private static readonly IModelConverter[] _converters =
    [
        new NullableModelConverter(),
        new EnumModelConverter(),
        new ModelObjectModelConverter(),
        new TupleModelConverter(),
        new KeyValuePairModelConverter(),
        new ArrayModelConverter(),
        new ListModelConverter(),
        new HashSetModelConverter(),
        new SortedSetModelConverter(),
        new DictionaryModelConverter(),
        new SortedDictionaryModelConverter(),
        new ImmutableArrayModelConverter(),
        new ImmutableListModelConverter(),
        new ImmutableHashSetModelConverter(),
        new ImmutableSortedSetModelConverter(),
        new ImmutableDictionaryModelConverter(),
        new ImmutableSortedDictionaryModelConverter(),
    ];

    public static Type GetType(Type type, int version)
    {
        try
        {
            return version is 0 ? type : GetTypes(type)[version - 1];
        }
        catch (Exception e) when (e is not ModelException)
        {
            throw new ModelException($"Failed to get type for {type} with version {version}", e);
        }
    }

    public static (string TypeName, int Version) GetTypeInfo(Type type)
    {
        try
        {
            if (type.IsDefined(typeof(ModelAttribute)) || type.IsDefined(typeof(OriginModelAttribute)))
            {
                var types = GetTypes(type);
                return (TypeUtility.GetTypeName(types[^1]), types.IndexOf(type) + 1);
            }

            return (TypeUtility.GetTypeName(type), 0);
        }
        catch (Exception e) when (e is not ModelException)
        {
            var message = $"Type '{type}' is not supported or not registered in known types.";
            throw new InvalidModelException(message, type, e);
        }
    }

    public static int GetVersion(Type type)
    {
        try
        {
            if (type.IsDefined(typeof(ModelAttribute)) || type.IsDefined(typeof(OriginModelAttribute)))
            {
                return GetTypes(type).IndexOf(type) + 1;
            }
            else if (TypeUtility.IsKnownType(type))
            {
                return 0;
            }

            var message = $"Type '{type}' is not supported or not registered in known types.";
            throw new InvalidModelException(message, type);
        }
        catch (Exception e) when (e is not ModelException)
        {
            var message = $"Type '{type}' is not supported or not registered in known types.";
            throw new InvalidModelException(message, type, e);
        }
    }

    public static ModelPropertyCollection GetProperties(Type type)
    {
        try
        {
            if (!type.IsDefined(typeof(ModelAttribute)) && !type.IsDefined(typeof(OriginModelAttribute)))
            {
                throw new ArgumentException($"Type '{type}' does not have the {nameof(ModelAttribute)}", nameof(type));
            }

            return _propertiesByType.GetOrAdd(type, CreateProperties);
        }
        catch (Exception e) when (e is not ModelException)
        {
            throw new ModelException($"Failed to get properties for {type}", e);
        }
    }

    public static IModelConverter GetConverter(Type type) => _converterByType.GetOrAdd(type, CreateConverter);

    public static bool TryGetConverter(Type type, [MaybeNullWhen(false)] out IModelConverter converter)
    {
        lock (_lock)
        {
            if (FindConverter(type) is { } foundConverter)
            {
                converter = foundConverter;
                return true;
            }

            converter = null;
            return converter is not null;
        }

        static IModelConverter? FindConverter(Type type)
        {
            if (_converterByType.TryGetValue(type, out var converter))
            {
                return converter;
            }

            if (type.IsDefined(typeof(ModelConverterAttribute)))
            {
                return GetConverter(type);
            }

            converter = _converters.FirstOrDefault(converter => converter.CanConvert(type));
            if (converter is not null)
            {
                _converterByType.TryAdd(type, converter);
                return converter;
            }

            return null;
        }
    }

    public static void AddConverter(Type type, IModelConverter converter) => _converterByType.TryAdd(type, converter);

    public static bool Equals<T>(T left, T? right) => Equals(left, right, typeof(T));

    public static bool Equals(object? obj1, object? obj2, Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlyingType)
        {
            return Equals(obj1, obj2, underlyingType);
        }

        if (ReferenceEquals(obj1, obj2))
        {
            return true;
        }

        if (obj1 is null || obj2 is null)
        {
            return obj1 == obj2;
        }

        if (obj1.GetType() != obj2.GetType())
        {
            return false;
        }

        var objType = obj1.GetType() != type ? obj1.GetType() : type;
        if (FindComparer(objType) is { } comparer)
        {
            return comparer.Equals(obj1, obj2, objType);
        }

        return object.Equals(obj1, obj2);
    }

    public static int GetHashCode<T>(T obj) => GetHashCode(obj, typeof(T));

    public static int GetHashCode(object? obj, Type type)
    {
        if (obj is null)
        {
            return 0;
        }

        if (FindComparer(type) is { } comparer)
        {
            return comparer.GetHashCode(obj, type);
        }

        return obj.GetHashCode();
    }

    public static void Validate(object obj, ModelOptions options)
    {
        Validator.ValidateObject(
            instance: obj,
            validationContext: new ValidationContext(obj, options, items: null),
            validateAllProperties: true);
    }

    private static ModelPropertyCollection CreateProperties(Type type)
    {
        var builder = ImmutableArray.CreateBuilder<ModelProperty>();
        var currentType = type;
        while (currentType is not null)
        {
            var properties = _declaredPropertiesByType.GetOrAdd(currentType, CreateDeclaredProperties);
            builder.InsertRange(0, properties);
            currentType = currentType.BaseType;
        }

        return new(builder.ToImmutable());

        static ModelPropertyCollection CreateDeclaredProperties(Type type) => new(type);
    }

    private static ImmutableArray<Type> GetTypes(Type type)
    {
        if (type.GetCustomAttribute<OriginModelAttribute>() is { } originModelAttribute)
        {
            return _typesByType.GetOrAdd(originModelAttribute.Type, CreateTypes);
        }

        return _typesByType.GetOrAdd(type, CreateTypes);
    }

    private static ImmutableArray<Type> CreateTypes(Type type)
    {
        var query = from attribute in type.GetCustomAttributes<ModelHistoryAttribute>()
                    orderby attribute.Version
                    select attribute;
        var attributes = query.ToArray();
        var builder = ImmutableArray.CreateBuilder<Type>(attributes.Length + 1);
        var modelAttribute = type.GetCustomAttribute<ModelAttribute>()
            ?? throw new InvalidModelException($"Type '{type}' does not have the {nameof(ModelAttribute)}.", type);

        Type? previousType = null;
        var previousVersion = 0;
        for (var i = 0; i < attributes.Length; i++)
        {
            var attribute = attributes[i];
            var attributeType = attribute.Type;
            var attributeVersion = attribute.Version;
            attribute.Validate(type, previousVersion, previousType);

            if (builder.Contains(attributeType))
            {
                throw new InvalidModelException(
                    $"Type '{attributeType}' is already defined for '{type}'.", type);
            }

            builder.Add(attributeType);
            previousType = attributeType;
            previousVersion = attributeVersion;
        }

        modelAttribute.Validate(type, previousVersion, previousType);

        builder.Add(type);

        return builder.ToImmutable();
    }

    private static IModelComparer? FindComparer(Type type)
    {
        if (TryGetConverter(type, out var converter) && converter is IModelComparer comparer)
        {
            return comparer;
        }

        return null;
    }

    private static IModelConverter CreateConverter(Type type)
    {
        if (type.GetCustomAttribute<ModelConverterAttribute>() is not { } attribute)
        {
            throw new ArgumentException(
                $"Type {type} does not have a ModelConverterAttribute", nameof(type));
        }

        var converterType = attribute.ConverterType;
        var constructorWithType = converterType.GetConstructor([typeof(Type)]);
        if (constructorWithType is not null)
        {
            if (constructorWithType.Invoke([type]) is not IModelConverter converter)
            {
                throw new UnreachableException($"Cannot create converter for {type} using {converterType}");
            }

            return converter;
        }
        else
        {
            if (Activator.CreateInstance(converterType) is not IModelConverter converter)
            {
                throw new UnreachableException($"Cannot create converter for {type} using {converterType}");
            }

            return converter;
        }
    }
}
