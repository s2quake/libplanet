namespace Libplanet.Serialization.Json;

internal abstract class ModelJsonDescriptor
{
    public abstract bool CanSerialize(Type type);

    public abstract (string, Type)[] GetTypes(Type type, out bool isArray);

    public abstract object?[] Serialize(object obj, Type type, ModelOptions options);

    public abstract object Deserialize(Type type, object?[] values, ModelOptions options);

    public abstract bool Equals(object obj1, object obj2, Type type);

    public abstract int GetHashCode(object obj, Type type);
}
