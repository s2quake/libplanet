using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model("BlockHashResponseMessage", Version = 1)]
internal sealed partial record class BlockHashResponseMessage : MessageBase
{
    [Property(0)]
    public ImmutableArray<BlockHash> BlockHashes { get; init; } = [];
}
