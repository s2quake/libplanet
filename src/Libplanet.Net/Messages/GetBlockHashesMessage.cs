using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model(Version = 1)]
internal sealed record class GetBlockHashesMessage : MessageContent
{
    [Property(0)]
    public required BlockHash BlockHash { get; init; }

    public override MessageType Type => MessageType.GetBlockHashes;
}
