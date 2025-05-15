namespace Libplanet.Serialization;

public abstract class ModelDescriptor
{
    public abstract bool CanSerialize(Type type);

    public abstract IEnumerable<Type> GetTypes(Type type, int length);

    public abstract IEnumerable<(Type Type, object? Value)> GetValues(object obj, Type type);

    public abstract object CreateInstance(Type type, IEnumerable<object?> values);

    public abstract bool Equals(object obj1, object obj2, Type type);

    public abstract int GetHashCode(object obj, Type type);
}
