using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using static Libplanet.Serialization.ArrayUtility;

namespace Libplanet.Serialization;

public sealed class ModelResolver : IModelResolver
{
    public static readonly ModelResolver Default = new();

    private static readonly ConcurrentDictionary<Type, ImmutableArray<PropertyInfo>> _propertiesByType = [];
    private static readonly ConcurrentDictionary<Type, ImmutableArray<Type>> _typesByType = [];

    Type IModelResolver.GetType(Type type, int version) => GetType(type, version);

    string IModelResolver.GetTypeName(Type type) => TypeUtility.GetTypeName(type);

    int IModelResolver.GetVersion(Type type)
    {
        if (type.IsDefined(typeof(ModelAttribute)) || type.IsDefined(typeof(LegacyModelAttribute)))
        {
            return GetTypes(type).IndexOf(type) + 1;
        }

        return 0;
    }

    ImmutableArray<PropertyInfo> IModelResolver.GetProperties(Type type)
    {
        if (!type.IsDefined(typeof(ModelAttribute)) && !type.IsDefined(typeof(LegacyModelAttribute)))
        {
            throw new ArgumentException(
                $"Type {type} does not have {nameof(ModelAttribute)}", nameof(type));
        }

        return _propertiesByType.GetOrAdd(type, CreateProperties);
    }

    internal static ImmutableArray<PropertyInfo> CreateProperties(Type type)
    {
        var query = from propertyInfo in type.GetProperties()
                    let propertyAttribute
                        = propertyInfo.GetCustomAttribute<PropertyAttribute>()
                    where propertyAttribute is not null
                    orderby propertyAttribute.Index
                    select (propertyInfo, propertyAttribute);
        var items = query.ToArray();
        var builder = ImmutableArray.CreateBuilder<PropertyInfo>(items.Length);
        foreach (var (propertyInfo, propertyAttribute) in items)
        {
            var index = propertyAttribute.Index;
            if (index != builder.Count)
            {
                throw new NotSupportedException(
                    $"Property {propertyInfo.Name} has an invalid index {index}");
            }

            ValidatorProperty(type, propertyInfo);

            builder.Add(propertyInfo);
        }

        return builder.ToImmutable();
    }

    internal static ImmutableArray<Type> GetTypes(Type type)
        => _typesByType.GetOrAdd(type, CreateTypes);

    private static void ValidatorProperty(Type type, PropertyInfo propertyInfo)
    {
        var propertyType = propertyInfo.PropertyType;
        if (typeof(IList).IsAssignableFrom(propertyType))
        {
            ValidateArrayProperty(type, propertyType);
        }
    }

    private static void ValidateArrayProperty(Type type, Type propertyType)
    {
        if (!IsStandardArrayType(propertyType))
        {
            throw new ModelSerializationException($"Type {propertyType} is not supported.");
        }

        var bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var equatableType = typeof(IEquatable<>).MakeGenericType(type);
        if (!equatableType.IsAssignableFrom(type))
        {
            throw new ModelSerializationException(
                $"Type {type} does not implement {equatableType}. " +
                "Please implement IEquatable<T> and override GetHashCode and Equals methods.");
        }

        var isRecord = type.GetMethod("<Clone>$") != null;
        var methodParams1 = new[] { type };
        var methodName1 = nameof(IEquatable<object>.Equals);
        var methodInfo1 = type.GetMethod(methodName1, bindingFlags, types: methodParams1);
        if (methodInfo1 is null)
        {
            throw new ModelSerializationException(
                $"Method {nameof(IEquatable<object>.Equals)} is not implemented in {type}. " +
                "Please implement IEquatable<T> Equals method.");
        }
        else if (methodInfo1.IsDefined(typeof(CompilerGeneratedAttribute)))
        {
            throw new ModelSerializationException(
                $"Method {nameof(IEquatable<object>.Equals)} is not implemented in {type}. " +
                "Please implement IEquatable<T> Equals method.");
        }

        var methodName2 = nameof(GetHashCode);
        var methodInfo2 = type.GetMethod(methodName2, bindingFlags);
        if (methodInfo2 is null)
        {
            throw new ModelSerializationException(
                $"Method {nameof(GetHashCode)} is not implemented in {type}. " +
                "Please override GetHashCode method.");
        }
        else if (methodInfo2.DeclaringType != type
            && methodInfo2.IsDefined(typeof(CompilerGeneratedAttribute)))
        {
            throw new ModelSerializationException(
                $"Method {nameof(GetHashCode)} is not implemented in {type}. " +
                "Please override GetHashCode method.");
        }

        if (!isRecord)
        {
            var methodParams3 = new[] { typeof(object) };
            var methodName3 = nameof(object.Equals);
            var methodInfo3 = type.GetMethod(methodName3, bindingFlags, types: methodParams3);
            if (methodInfo3 is null)
            {
                throw new ModelSerializationException(
                    $"Method {nameof(object.Equals)} is not implemented in {type}. " +
                    "Please override Equals method.");
            }
        }
    }

    private static Type GetType(Type type, int version) => version is 0 ? type : GetTypes(type)[version - 1];

    private static ImmutableArray<Type> CreateTypes(Type type)
    {
        var query = from attribute in type.GetCustomAttributes<ModelAttribute>()
                    orderby attribute.Version
                    select attribute;
        var attributes = query.ToArray();
        var builder = ImmutableArray.CreateBuilder<Type>(attributes.Length + 1);

        Type? previousType = null;
        for (var i = 0; i < attributes.Length; i++)
        {
            var attribute = attributes[i];
            var validationContext = new ValidationContext(attribute);
            Validator.ValidateObject(attribute, validationContext, validateAllProperties: true);
            var version = attribute.Version;
            if (version != i + 1)
            {
                throw new ArgumentException(
                    $"Version of {type} must be sequential starting from 1", nameof(type));
            }

            if (version == attributes.Length)
            {
                if (attribute.Type is not null)
                {
                    throw new ArgumentException("Last version must not have a type", nameof(type));
                }
            }
            else
            {
                if (attribute.Type is null)
                {
                    throw new ArgumentException(
                        $"Version {version} of {type} must have a type", nameof(type));
                }

                if (attribute.Type.GetCustomAttribute<LegacyModelAttribute>() is not { } legacyModelAttribute)
                {
                    throw new ArgumentException(
                        $"Type {attribute.Type} does not have {nameof(LegacyModelAttribute)}",
                        nameof(type));
                }

                if (legacyModelAttribute.OriginType != type)
                {
                    throw new ArgumentException("OriginType of LegacyModelAttribute is not valid", nameof(type));
                }
            }

            var currentType = attribute.Type ?? type;
            if (previousType is not null)
            {
                if (currentType.GetConstructor([previousType]) is null)
                {
                    throw new ArgumentException(
                        $"Type {currentType} does not have a constructor with {previousType}", nameof(type));
                }

                if (currentType.GetConstructor([]) is null)
                {
                    throw new ArgumentException(
                        $"Type {currentType} does not have a default constructor", nameof(type));
                }
            }

            if (builder.Contains(currentType))
            {
                throw new ArgumentException(
                    $"Type {currentType} is already registered", nameof(type));
            }

            builder.Add(currentType);
            previousType = currentType;
        }

        builder.Add(type);

        return builder.ToImmutable();
    }
}
