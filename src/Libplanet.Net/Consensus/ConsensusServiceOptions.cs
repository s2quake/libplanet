using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Libplanet.Net.Consensus;

public sealed record class ConsensusServiceOptions
{
    public ImmutableHashSet<Peer> KnownPeers { get; init; } = [];

    public TimeSpan BlockInterval { get; init; }

    public ConsensusOptions ConsensusOptions { get; init; } = ConsensusOptions.Default;

    public ILogger<ConsensusService> Logger { get; init; } = NullLogger<ConsensusService>.Instance;
}
