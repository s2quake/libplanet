using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public readonly record struct ContextState
{
    public int VoteCount { get; init; }

    public int Height { get; init; }

    public int Round { get; init; }

    public ConsensusStep Step { get; init; }

    public BlockHash? Proposal { get; init; }
}

