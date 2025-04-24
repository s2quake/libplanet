namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class LegacyModelAttribute : Attribute
{
    public required int Version { get; init; }

    public required Type Type { get; init; }
}
