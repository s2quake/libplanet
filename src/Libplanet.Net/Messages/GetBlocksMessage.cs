using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "GetBlocksMessage")]
internal sealed partial record class GetBlocksMessage : MessageContent
{
    [Property(0)]
    public ImmutableArray<BlockHash> BlockHashes { get; init; }

    [Property(1)]
    public int ChunkSize { get; init; }

    public override MessageType Type => MessageType.GetBlocks;
}
