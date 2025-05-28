using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed partial record class BlockHashesMessage : MessageContent
{
    public override MessageType Type => MessageType.BlockHashes;

    [Property(0)]
    public ImmutableArray<BlockHash> Hashes { get; init; } = [];
}
