using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "BlockchainStateResponseMessage")]
internal sealed record class BlockchainStateResponseMessage : MessageBase
{
    [Property(0)]
    public required BlockSummary Genesis { get; init; }

    [Property(1)]
    public required BlockSummary Tip { get; init; }
}
