using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net.Messages;

[Model("PeerRequestMessage", Version = 1)]
public sealed record class PeerRequestMessage : MessageBase
{
    [Property(0)]
    public Address Target { get; init; }

    [Property(1)]
    public int K { get; init; } = PeerCollection.BucketCount;
}
