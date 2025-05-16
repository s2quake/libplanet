namespace Libplanet.Serialization;

public abstract class ModelDescriptor
{
    public abstract bool CanSerialize(Type type);

    public abstract Type[] GetTypes(Type type, out bool isArray);

    public abstract object?[] GetValues(object obj, Type type);

    public abstract object CreateInstance(Type type, object?[] values);

    public abstract bool Equals(object obj1, object obj2, Type type);

    public abstract int GetHashCode(object obj, Type type);
}
