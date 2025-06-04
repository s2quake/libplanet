using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class ModelHistoryAttribute : Attribute
{
    [NonNegative]
    public required int Version { get; init; }

    public required Type Type { get; init; }
}
