using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Net.Consensus;

public sealed record class ConsensusOptions
{
    public static ConsensusOptions Default { get; } = new();

    [Positive]
    public TimeSpan ProposeTimeoutBase { get; init; } = TimeSpan.FromSeconds(8);

    [Positive]
    public int PreVoteTimeoutBase { get; init; } = 1_000;

    [Positive]
    public int PreCommitTimeoutBase { get; init; } = 1_000;

    [Positive]
    public TimeSpan ProposeTimeoutDelta { get; init; } = TimeSpan.FromSeconds(4);

    [Positive]
    public int PreVoteTimeoutDelta { get; init; } = 500;

    [Positive]
    public int PreCommitTimeoutDelta { get; init; } = 500;

    [NonNegative]
    public int EnterPreVoteDelay { get; init; }

    [NonNegative]
    public int EnterPreCommitDelay { get; init; }

    [NonNegative]
    public int EnterEndCommitDelay { get; init; }

    public TimeSpan TimeoutPropose(int round) => ProposeTimeoutBase + (ProposeTimeoutDelta * round);

    public TimeSpan TimeoutPreVote(int round)
        => TimeSpan.FromMilliseconds(PreVoteTimeoutBase + (PreVoteTimeoutDelta * round));

    public TimeSpan TimeoutPreCommit(int round)
        => TimeSpan.FromMilliseconds(PreCommitTimeoutBase + (PreCommitTimeoutDelta * round));
}
