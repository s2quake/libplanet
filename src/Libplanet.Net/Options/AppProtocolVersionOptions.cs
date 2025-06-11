using Libplanet.Types;

namespace Libplanet.Net.Options;

public sealed record class AppProtocolVersionOptions
{
    public ProtocolVersion AppProtocolVersion { get; init; }

    public ImmutableHashSet<PublicKey> TrustedAppProtocolVersionSigners { get; init; } = [];

    public DifferentAppProtocolVersionEncountered DifferentAppProtocolVersionEncountered { get; init; }
        = (peer, peerVersion, localVersion) => { };
}
