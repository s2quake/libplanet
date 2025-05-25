using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class GetBlocksMessage : MessageContent
{
    [Property(0)]
    public ImmutableArray<BlockHash> BlockHashes { get; init; }

    [Property(1)]
    public int ChunkSize { get; init; }

    public override MessageType Type => MessageType.GetBlocks;
}
