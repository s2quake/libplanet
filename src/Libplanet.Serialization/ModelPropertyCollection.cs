using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Libplanet.Serialization;

public sealed class ModelPropertyCollection : IEnumerable<ModelProperty>
{
    private readonly ImmutableArray<ModelProperty> _items;
    private readonly Dictionary<string, ModelProperty> _itemByName;

    public ModelPropertyCollection(Type type)
    {
        var bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var query = from propertyInfo in type.GetProperties(bindingFlags)
                    let propertyAttribute = propertyInfo.GetCustomAttribute<PropertyAttribute>()
                    where propertyAttribute is not null
                    orderby propertyAttribute.Index
                    select (propertyAttribute, propertyInfo);
        var items = query.ToArray();
        var builder = ImmutableArray.CreateBuilder<ModelProperty>(items.Length);
        var hasArrayProperty = false;
        foreach (var (propertyAttribute, propertyInfo) in items)
        {
            var index = propertyAttribute.Index;
            var propertyType = propertyInfo.PropertyType;
            if (index != builder.Count)
            {
                throw new NotSupportedException(
                    $"Property {propertyInfo.Name} of {type} has an invalid index {index}. ");
            }

            if (typeof(IEnumerable).IsAssignableFrom(propertyType) && propertyType != typeof(string))
            {
                hasArrayProperty = true;
            }

            builder.Add(new(propertyAttribute, propertyInfo));
        }

        if (hasArrayProperty)
        {
            ValidateAsEquatable(type);
        }

        _items = builder.ToImmutable();
        _itemByName = _items.ToDictionary(item => item.Name, p => p);
    }

    internal ModelPropertyCollection(in ImmutableArray<ModelProperty> items)
    {
        _items = [.. items];
        _itemByName = _items.ToDictionary(item => item.Name, p => p);
    }

    public ModelProperty this[int index] => _items[index];

    public ModelProperty this[string name] => _itemByName[name];

    public int Count => _items.Length;

    public bool Contains(string name) => _itemByName.ContainsKey(name);

    public IEnumerator<ModelProperty> GetEnumerator()
    {
        foreach (var item in _items)
        {
            yield return item;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static void ValidateAsEquatable(Type type)
    {
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
}
