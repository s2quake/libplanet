using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "BlockHashMessage")]
internal sealed partial record class BlockHashMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<BlockHash> BlockHashes { get; init; } = [];
}
