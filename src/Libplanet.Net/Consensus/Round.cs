using Libplanet.Types;

namespace Libplanet.Net.Consensus;

internal sealed class Round(int height, int index, ImmutableSortedSet<Validator> validators)
{
    public int Height { get; } = ValidateHeight(height);

    public int Index { get; } = ValidateRound(index);

    public Proposal? Proposal { get; set; }

    public VoteCollection PreVotes { get; } = new(height, index, VoteType.PreVote, validators);

    public VoteCollection PreCommits { get; } = new(height, index, VoteType.PreCommit, validators);

    public MajorityBox PreVoteMajorities { get; } = new(height, index, VoteType.PreVote, validators);

    public MajorityBox PreCommitMajorities { get; } = new(height, index, VoteType.PreCommit, validators);

    public bool IsQuorumReached { get; set; }

    public bool IsPreVoteTimeoutScheduled { get; set; }

    public bool IsPreCommitTimeoutScheduled { get; set; }

    public bool IsPreCommitWaitScheduled { get; set; }

    public bool IsEndCommitWaitScheduled { get; set; }

    private static int ValidateHeight(int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 0);
        return height;
    }

    private static int ValidateRound(int round)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(round, 0);
        return round;
    }
}
