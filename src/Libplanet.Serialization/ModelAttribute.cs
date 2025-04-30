using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class ModelAttribute : Attribute
{
    [NonNegative]
    public required int Version { get; init; }

    public Type? Type { get; init; }
}
