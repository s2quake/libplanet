using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "BlockRequestMessage")]
internal sealed partial record class BlockRequestMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<BlockHash> BlockHashes { get; init; }

    [Property(1)]
    public int ChunkSize { get; init; }
}
