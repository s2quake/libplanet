namespace Libplanet.Net;

public sealed record class PeerState
{
    public required Peer Peer { get; init; }

    public required DateTimeOffset LastUpdated { get; init; }

    public DateTimeOffset LastChecked { get; init; }

    public TimeSpan Latency { get; init; }
}
