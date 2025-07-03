using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "BlockHeaderMessage")]
internal sealed record class BlockHeaderMessage : MessageBase
{
    [Property(0)]
    public required BlockHash GenesisHash { get; init; }

    [Property(1)]
    public required BlockSummary BlockSummary { get; init; }

    public int HeaderIndex => BlockSummary.Height;

    public BlockHash HeaderHash => BlockSummary.BlockHash;
}
