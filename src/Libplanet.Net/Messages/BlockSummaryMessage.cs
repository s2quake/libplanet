using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "BlockSummaryMessage")]
internal sealed record class BlockSummaryMessage : MessageBase
{
    [Property(0)]
    public required BlockHash GenesisBlockHash { get; init; }

    [Property(1)]
    public required BlockSummary BlockSummary { get; init; }
}
