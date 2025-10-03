using Libplanet.Serialization;

namespace Libplanet.Net.Messages;

[Model("BlockchainStateResponseMessage", Version = 1)]
internal sealed record class BlockchainStateResponseMessage : MessageBase
{
    [Property(0)]
    public required BlockSummary Genesis { get; init; }

    [Property(1)]
    public required BlockSummary Tip { get; init; }
}
