using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;

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

    public ImmutableArray<IObjectValidator<Block>> BlockValidators { get; init; } = [];

    internal TimeSpan TimeoutPropose(Round round) => ProposeTimeoutBase + (ProposeTimeoutDelta * round.Index);

    internal TimeSpan TimeoutPreVote(Round round)
        => TimeSpan.FromMilliseconds(PreVoteTimeoutBase + (PreVoteTimeoutDelta * round.Index));

    internal TimeSpan TimeoutPreCommit(Round round)
        => TimeSpan.FromMilliseconds(PreCommitTimeoutBase + (PreCommitTimeoutDelta * round.Index));

    internal void ValidateBlock(Block block)
    {
        foreach (var validator in BlockValidators)
        {
            validator.Validate(block);
        }
    }
}
