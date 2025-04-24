using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using static Libplanet.Serialization.ArrayUtility;

namespace Libplanet.Serialization;

public sealed class ModelResolver : IModelResolver
{
    public static readonly ModelResolver Default = new();

    private static readonly ConcurrentDictionary<Type, ImmutableArray<PropertyInfo>>
        _propertiesByType = [];

    private static readonly ConcurrentDictionary<Type, ImmutableArray<Type>>
        _typesByType = [];

    Type IModelResolver.GetType(Type type, int version) => GetType(type, version);

    string IModelResolver.GetTypeName(Type type)
    {
        var typeName = type.FullName
         ?? throw new UnreachableException("Type does not have FullName");
        var assemblyName = type.Assembly.GetName().Name
             ?? throw new UnreachableException("Assembly does not have Name");
        return $"{typeName}, {assemblyName}";
    }

    int IModelResolver.GetVersion(Type type)
    {
        var attribute = type.GetCustomAttribute<ModelAttribute>()
            ?? throw new UnreachableException($"Type does not have {nameof(ModelAttribute)}");

        if (attribute.Version < 1)
        {
            throw new ArgumentException(
                $"Version of {type} must be greater than or equal to 1", nameof(type));
        }

        return attribute.Version;
    }

    ImmutableArray<PropertyInfo> IModelResolver.GetProperties(Type type)
    {
        if (!type.IsDefined(typeof(ModelAttribute)))
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

    private static Type GetType(Type type, int version) => GetTypes(type)[version - 1];

    private static ImmutableArray<Type> CreateTypes(Type type)
    {
        var modelAttribute = type.GetCustomAttribute<ModelAttribute>()
            ?? throw new UnreachableException($"Type does not have {nameof(ModelAttribute)}");
        var query = from attribute in type.GetCustomAttributes<LegacyModelAttribute>()
                    orderby attribute.Version
                    select attribute;
        var attributes = query.ToArray();
        var builder = ImmutableArray.CreateBuilder<Type>(attributes.Length + 1);

        if (modelAttribute.Version != attributes.Length + 1)
        {
            throw new ArgumentException(
                $"Version of {type} must be sequential starting from 1", nameof(type));
        }

        for (var i = 0; i < attributes.Length; i++)
        {
            var attribute = attributes[i];
            var version = attribute.Version;
            if (version != i + 1)
            {
                throw new ArgumentException(
                    $"Version of {type} must be sequential starting from 1", nameof(type));
            }

            builder.Add(attribute.Type);
        }

        builder.Add(type);

        return builder.ToImmutable();
    }
}
