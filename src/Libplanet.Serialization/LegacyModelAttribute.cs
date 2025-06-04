namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class OriginModelAttribute : Attribute
{
    public required Type Type { get; init; }

    public bool AllowSerialization { get; set; }
}
