namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class ModelKnownTypeAttribute(Type type, string typeName) : Attribute
{
    public Type Type { get; } = type;

    public string TypeName { get; } = typeName;
}
