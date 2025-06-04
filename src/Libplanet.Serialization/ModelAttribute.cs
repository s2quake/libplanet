using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ModelAttribute : Attribute
{
    [NonNegative]
    public required int Version { get; init; }

    [NotEmpty]
    public required string TypeName { get; init; }
}
