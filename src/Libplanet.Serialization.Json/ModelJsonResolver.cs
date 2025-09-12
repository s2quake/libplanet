using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Libplanet.Serialization.Descriptors;
using Libplanet.Serialization.Json.Converters;
using Libplanet.Serialization.Json.Descriptors;
using Libplanet.Serialization.ModelConverters;

namespace Libplanet.Serialization.Json;

internal static class ModelJsonResolver
{
    private static readonly object _lock = new();
    // private static readonly ConcurrentDictionary<Type, ImmutableArray<ModelProperty>> _declaredPropertiesByType = [];
    // private static readonly ConcurrentDictionary<Type, ImmutableArray<ModelProperty>> _propertiesByType = [];
    // private static readonly ConcurrentDictionary<Type, ImmutableArray<Type>> _typesByType = [];
    private static readonly ConcurrentDictionary<Type, ModelJsonDescriptor> _descriptorByType = [];
    private static readonly ConcurrentDictionary<Type, JsonConverter> _converterByType = new()
    {
        [typeof(BigInteger)] = new BigIntegerJsonConverter(),
        [typeof(bool)] = new BooleanJsonConverter(),
        [typeof(byte)] = new ByteJsonConverter(),
        [typeof(char)] = new CharJsonConverter(),
        [typeof(DateTimeOffset)] = new DateTimeOffsetJsonConverter(),
        [typeof(Guid)] = new GuidJsonConverter(),
        [typeof(int)] = new Int32JsonConverter(),
        [typeof(long)] = new Int64JsonConverter(),
        [typeof(string)] = new StringJsonConverter(),
        [typeof(TimeSpan)] = new TimeSpanJsonConverter(),
    };
    private static readonly ModelJsonDescriptor[] _descriptors =
    [
        // new ArrayModelJsonDescriptor(),
        new ObjectModelJsonDescriptor(),
        // new KeyValuePairModelJsonDescriptor(),
        // new ListModelJsonDescriptor(),
        // new DictionaryModelJsonDescriptor(),
        // new ImmutableArrayModelJsonDescriptor(),
        // new ImmutableListModelJsonDescriptor(),
        // new ImmutableSortedSetModelJsonDescriptor(),
        // new ImmutableDictionaryModelJsonDescriptor(),
        // new ImmutableSortedDictionaryModelJsonDescriptor(),
        // new TupleModelJsonDescriptor(),
    ];

    // public static Type GetType(Type type, int version)
    // {
    //     try
    //     {
    //         return version is 0 ? type : GetTypes(type)[version - 1];
    //     }
    //     catch (Exception e)
    //     {
    //         throw new ModelSerializationException($"Failed to get type for {type} with version {version}", e);
    //     }
    // }

    public static string GetTypeName(Type type) => ModelResolver.GetTypeName(type);

    public static int GetVersion(Type type) => ModelResolver.GetVersion(type);

    public static ModelJsonDescriptor GetDescriptor(Type type)
    {
        if (FindDescriptor(type) is { } descriptor)
        {
            return descriptor;
        }

        throw new NotSupportedException($"Type {type} is not supported.");
    }

    public static bool TryGetDescriptor(Type type, [MaybeNullWhen(false)] out ModelJsonDescriptor descriptor)
    {
        if (FindDescriptor(type) is { } foundDescriptor)
        {
            descriptor = foundDescriptor;
            return true;
        }

        descriptor = null;
        return descriptor is not null;
    }

    // public static ImmutableArray<ModelProperty> GetProperties(Type type)
    // {
    //     try
    //     {
    //         if (!type.IsDefined(typeof(ModelAttribute)) && !type.IsDefined(typeof(OriginModelAttribute)))
    //         {
    //             throw new ArgumentException(
    //                 $"Type {type} does not have {nameof(ModelAttribute)}", nameof(type));
    //         }

    //         return _propertiesByType.GetOrAdd(type, CreateProperties);
    //     }
    //     catch (Exception e)
    //     {
    //         throw new ModelSerializationException($"Failed to get properties for {type}", e);
    //     }
    // }

    public static JsonConverter GetConverter(Type type) => _converterByType.GetOrAdd(type, CreateConverter);

    public static bool TryGetConverter(Type type, [MaybeNullWhen(false)] out JsonConverter converter)
    {
        if (_converterByType.TryGetValue(type, out converter))
        {
            return true;
        }

        if (type.IsDefined(typeof(JsonConverterAttribute)))
        {
            converter = GetConverter(type);
            return true;
        }

        converter = null;
        return converter is not null;
    }

    public static void AddConverter(Type type, JsonConverter converter) => _converterByType.TryAdd(type, converter);

    // public static bool Equals<T>(T left, T? right) => Equals(left, right, typeof(T));

    // public static bool Equals(object? obj1, object? obj2, Type type)
    // {
    //     if (Nullable.GetUnderlyingType(type) is { } underlyingType)
    //     {
    //         return Equals(obj1, obj2, underlyingType);
    //     }

    //     if (ReferenceEquals(obj1, obj2))
    //     {
    //         return true;
    //     }

    //     if (obj1 is null || obj2 is null)
    //     {
    //         return obj1 == obj2;
    //     }

    //     if (obj1.GetType() != obj2.GetType())
    //     {
    //         return false;
    //     }

    //     var objType = obj1.GetType() != type ? obj1.GetType() : type;
    //     if (FindDescriptor(objType) is { } descriptor)
    //     {
    //         return descriptor.Equals(obj1, obj2, objType);
    //     }

    //     return object.Equals(obj1, obj2);
    // }

    // public static int GetHashCode<T>(T obj) => GetHashCode(obj, typeof(T));

    // public static int GetHashCode(object? obj, Type type)
    // {
    //     if (obj is null)
    //     {
    //         return 0;
    //     }

    //     if (FindDescriptor(type) is { } descriptor)
    //     {
    //         return descriptor.GetHashCode(obj, type);
    //     }

    //     return obj.GetHashCode();
    // }

    // public static void Validate(object obj, ModelOptions options)
    // {
    //     Validator.ValidateObject(
    //         instance: obj,
    //         validationContext: new ValidationContext(obj, options, options.Items),
    //         validateAllProperties: true);
    // }

    // private static ImmutableArray<ModelProperty> CreateProperties(Type type)
    // {
    //     var builder = ImmutableArray.CreateBuilder<ModelProperty>();
    //     var currentType = type;
    //     while (currentType is not null)
    //     {
    //         var properties = _declaredPropertiesByType.GetOrAdd(currentType, CreateDeclaredProperties);
    //         builder.InsertRange(0, properties);
    //         currentType = currentType.BaseType;
    //     }

    //     return builder.ToImmutable();
    // }

    // private static ImmutableArray<ModelProperty> CreateDeclaredProperties(Type type)
    // {
    //     var bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    //     var query = from propertyInfo in type.GetProperties(bindingFlags)
    //                 let propertyAttribute = propertyInfo.GetCustomAttribute<PropertyAttribute>()
    //                 where propertyAttribute is not null
    //                 orderby propertyAttribute.Index
    //                 select (propertyAttribute, propertyInfo);
    //     var items = query.ToArray();
    //     var builder = ImmutableArray.CreateBuilder<ModelProperty>(items.Length);
    //     var hasArrayProperty = false;
    //     foreach (var (propertyAttribute, propertyInfo) in items)
    //     {
    //         var index = propertyAttribute.Index;
    //         var propertyType = propertyInfo.PropertyType;
    //         if (index != builder.Count)
    //         {
    //             throw new NotSupportedException(
    //                 $"Property {propertyInfo.Name} of {type} has an invalid index {index}. ");
    //         }

    //         if (typeof(IEnumerable).IsAssignableFrom(propertyType) && propertyType != typeof(string))
    //         {
    //             hasArrayProperty = true;
    //         }

    //         builder.Add(new(propertyAttribute, propertyInfo));
    //     }

    //     if (hasArrayProperty)
    //     {
    //         ValidateAsEquatable(type);
    //     }

    //     return builder.ToImmutable();
    // }

    // private static ImmutableArray<Type> GetTypes(Type type)
    // {
    //     if (type.GetCustomAttribute<OriginModelAttribute>() is { } originModelAttribute)
    //     {
    //         return _typesByType.GetOrAdd(originModelAttribute.Type, CreateTypes);
    //     }

    //     return _typesByType.GetOrAdd(type, CreateTypes);
    // }

    // private static void ValidateAsEquatable(Type type)
    // {
    //     var bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    //     var equatableType = typeof(IEquatable<>).MakeGenericType(type);
    //     if (!equatableType.IsAssignableFrom(type))
    //     {
    //         throw new ModelSerializationException(
    //             $"Type {type} does not implement {equatableType}. " +
    //             "Please implement IEquatable<T> and override GetHashCode and Equals methods.");
    //     }

    //     var isRecord = type.GetMethod("<Clone>$") != null;
    //     var methodParams1 = new[] { type };
    //     var methodName1 = nameof(IEquatable<object>.Equals);
    //     var methodInfo1 = type.GetMethod(methodName1, bindingFlags, types: methodParams1);
    //     if (methodInfo1 is null)
    //     {
    //         throw new ModelSerializationException(
    //             $"Method {nameof(IEquatable<object>.Equals)} is not implemented in {type}. " +
    //             "Please implement IEquatable<T> Equals method.");
    //     }
    //     else if (methodInfo1.IsDefined(typeof(CompilerGeneratedAttribute)))
    //     {
    //         throw new ModelSerializationException(
    //             $"Method {nameof(IEquatable<object>.Equals)} is not implemented in {type}. " +
    //             "Please implement IEquatable<T> Equals method.");
    //     }

    //     var methodName2 = nameof(GetHashCode);
    //     var methodInfo2 = type.GetMethod(methodName2, bindingFlags);
    //     if (methodInfo2 is null)
    //     {
    //         throw new ModelSerializationException(
    //             $"Method {nameof(GetHashCode)} is not implemented in {type}. " +
    //             "Please override GetHashCode method.");
    //     }
    //     else if (methodInfo2.DeclaringType != type
    //         && methodInfo2.IsDefined(typeof(CompilerGeneratedAttribute)))
    //     {
    //         throw new ModelSerializationException(
    //             $"Method {nameof(GetHashCode)} is not implemented in {type}. " +
    //             "Please override GetHashCode method.");
    //     }

    //     if (!isRecord)
    //     {
    //         var methodParams3 = new[] { typeof(object) };
    //         var methodName3 = nameof(object.Equals);
    //         var methodInfo3 = type.GetMethod(methodName3, bindingFlags, types: methodParams3);
    //         if (methodInfo3 is null)
    //         {
    //             throw new ModelSerializationException(
    //                 $"Method {nameof(object.Equals)} is not implemented in {type}. " +
    //                 "Please override Equals method.");
    //         }
    //     }
    // }

    // private static ImmutableArray<Type> CreateTypes(Type type)
    // {
    //     var query = from attribute in type.GetCustomAttributes<ModelHistoryAttribute>()
    //                 orderby attribute.Version
    //                 select attribute;
    //     var attributes = query.ToArray();
    //     var builder = ImmutableArray.CreateBuilder<Type>(attributes.Length + 1);
    //     var modelAttribute = type.GetCustomAttribute<ModelAttribute>()
    //         ?? throw new ArgumentException(
    //             $"Type {type} does not have {nameof(ModelAttribute)}", nameof(type));

    //     Type? previousType = null;
    //     var previousVersion = 0;
    //     for (var i = 0; i < attributes.Length; i++)
    //     {
    //         var attribute = attributes[i];
    //         var attributeType = attribute.Type;
    //         var attributeVersion = attribute.Version;
    //         attribute.Validate(type, previousVersion, previousType);

    //         if (builder.Contains(attributeType))
    //         {
    //             throw new ArgumentException(
    //                 $"Type {attributeType} is already registered", nameof(type));
    //         }

    //         builder.Add(attributeType);
    //         previousType = attributeType;
    //         previousVersion = attributeVersion;
    //     }

    //     modelAttribute.Validate(type, previousVersion, previousType);

    //     builder.Add(type);

    //     return builder.ToImmutable();
    // }

    private static ModelJsonDescriptor? FindDescriptor(Type type)
    {
        if (_descriptorByType.TryGetValue(type, out var descriptor))
        {
            return descriptor;
        }

        lock (_lock)
        {
            descriptor = _descriptors.FirstOrDefault(descriptor => descriptor.CanSerialize(type));
            if (descriptor is not null)
            {
                _descriptorByType.TryAdd(type, descriptor);
                return descriptor;
            }
        }

        return null;
    }

    private static JsonConverter CreateConverter(Type type)
    {
        if (type.GetCustomAttribute<JsonConverterAttribute>() is not { } attribute)
        {
            throw new ArgumentException(
                $"Type {type} does not have a JsonConverterAttribute", nameof(type));
        }

        var converterType = attribute.ConverterType;
        var constructorWithType = converterType.GetConstructor([typeof(Type)]);
        if (constructorWithType is not null)
        {
            if (constructorWithType.Invoke([type]) is not JsonConverter converter)
            {
                throw new UnreachableException($"Cannot create converter for {type} using {converterType}");
            }

            return converter;
        }
        else
        {
            if (Activator.CreateInstance(converterType) is not JsonConverter converter)
            {
                throw new UnreachableException($"Cannot create converter for {type} using {converterType}");
            }

            return converter;
        }
    }
}
