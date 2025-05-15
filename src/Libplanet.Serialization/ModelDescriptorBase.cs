namespace Libplanet.Serialization;

public abstract class ModelDescriptorBase
{
    public abstract bool CanSerialize(Type type);

    public abstract bool CanDeserialize(Type type);

    public abstract IEnumerable<Type> GetTypes(Type type, int length, ModelOptions options);

    public abstract IEnumerable<(Type Type, object? Value)> GetValues(object obj, Type type, ModelOptions options);

    public abstract object CreateInstance(Type type, IEnumerable<object?> values, ModelOptions options);
}
