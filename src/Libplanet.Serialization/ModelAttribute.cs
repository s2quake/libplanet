namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class ModelAttribute : Attribute
{
    public required int Version { get; init; }
}
