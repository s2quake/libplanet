using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1, TypeName = "BlockHashesMessage")]
internal sealed partial record class BlockHashesMessage : MessageBase
{
    public override MessageType Type => MessageType.BlockHashes;

    [Property(0)]
    public ImmutableArray<BlockHash> Hashes { get; init; } = [];
}
