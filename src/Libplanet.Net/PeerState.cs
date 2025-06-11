namespace Libplanet.Net;

public sealed record class PeerState
{
    public required BoundPeer Peer { get; init; }

    public required DateTimeOffset LastUpdated { get; init; }

    public DateTimeOffset LastChecked { get; init; }

    public TimeSpan Latency { get; init; }
}
