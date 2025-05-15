namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class LegacyModelAttribute : Attribute
{
    public required Type OriginType { get; init; }

    public bool AllowSerialization { get; set; }
}
