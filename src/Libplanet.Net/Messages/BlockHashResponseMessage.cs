using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "BlockHashResponseMessage")]
internal sealed partial record class BlockHashResponseMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<BlockHash> BlockHashes { get; init; } = [];
}
