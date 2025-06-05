namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class ModelKnownTypeAttribute(Type type) : Attribute
{
    public Type Type { get; } = type;
}
