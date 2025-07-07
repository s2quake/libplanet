using Libplanet.Types;

namespace Libplanet.Net;

public sealed record class PeerState : IComparable<PeerState>
{
    public required Peer Peer { get; init; }

    public required DateTimeOffset LastUpdated { get; init; }

    public Address Address => Peer.Address;

    public TimeSpan Latency { get; init; }

    public int CompareTo(PeerState? other)
    {
        if (other is null)
        {
            return 1;
        }

        return LastUpdated.CompareTo(other.LastUpdated);
    }
}
