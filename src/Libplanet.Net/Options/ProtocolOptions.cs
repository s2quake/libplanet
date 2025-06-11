using Libplanet.Types;

namespace Libplanet.Net.Options;

public sealed record class ProtocolOptions
{
    public Protocol Protocol { get; init; }

    public ImmutableSortedSet<Address> AllowedSigners { get; init; } = [];

    public DifferentAppProtocolVersionEncountered DifferentAppProtocolVersionEncountered { get; init; }
        = (peer, peerVersion, localVersion) => { };
}
