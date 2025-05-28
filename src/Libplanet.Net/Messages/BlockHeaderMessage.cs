using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed partial record class BlockHeaderMessage : MessageContent
{
    [Property(0)]
    public required BlockHash GenesisHash { get; init; }

    [Property(1)]
    public required BlockExcerpt Excerpt { get; init; }

    public int HeaderIndex => Excerpt.Height;

    public BlockHash HeaderHash => Excerpt.BlockHash;

    public override MessageType Type => MessageType.BlockHeaderMessage;
}
