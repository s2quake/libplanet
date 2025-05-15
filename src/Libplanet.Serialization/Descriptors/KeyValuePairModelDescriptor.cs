using System.Diagnostics;

namespace Libplanet.Serialization.Descriptors;

internal sealed class KeyValuePairModelDescriptor : ModelDescriptorBase
{
    public override bool CanSerialize(Type type) => IsKeyValuePair(type);

    public override bool CanDeserialize(Type type) => IsKeyValuePair(type);

    public override IEnumerable<Type> GetTypes(Type type, int length, ModelOptions options)
    {
        var genericArguments = type.GetGenericArguments();
        if (genericArguments.Length != length)
        {
            throw new ModelSerializationException(
                $"The number of generic arguments {genericArguments.Length} does not match " +
                $"the number of tuple items {length}");
        }

        for (var i = 0; i < genericArguments.Length; i++)
        {
            yield return genericArguments[i];
        }
    }

    public override IEnumerable<(Type Type, object? Value)> GetValues(object obj, Type type, ModelOptions options)
    {
        var genericArguments = type.GetGenericArguments();
        if (type.GetProperty(nameof(KeyValuePair<string, string>.Key)) is not { } keyProperty)
        {
            throw new UnreachableException("Key property not found");
        }

        yield return (genericArguments[0], keyProperty.GetValue(obj));

        if (type.GetProperty(nameof(KeyValuePair<string, string>.Value)) is not { } valueProperty)
        {
            throw new UnreachableException("Value property not found");
        }

        yield return (genericArguments[1], valueProperty.GetValue(obj));
    }

    public override object CreateInstance(Type type, IEnumerable<object?> values, ModelOptions options)
        => TypeUtility.CreateInstance(type, args: [.. values]);

    private static bool IsKeyValuePair(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var genericTypeDefinition = type.GetGenericTypeDefinition();
        return genericTypeDefinition == typeof(KeyValuePair<,>);
    }
}
