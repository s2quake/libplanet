using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed partial record class GetBlockHashesMessage : MessageContent
{
    [Property(0)]
    public required BlockHash BlockHash { get; init; }

    public override MessageType Type => MessageType.GetBlockHashes;
}
