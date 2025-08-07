using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class Round(int height, int index, ImmutableSortedSet<Validator> validators)
{
    public int Height { get; } = ValidateHeight(height);

    public int Index { get; } = ValidateRound(index);

    public Proposal? Proposal { get; set; }

    public VoteCollection PreVotes { get; } = new(height, index, VoteType.PreVote, validators);

    public VoteCollection PreCommits { get; } = new(height, index, VoteType.PreCommit, validators);

    public MajorityBox PreVoteMajorities { get; } = new(height, index, VoteType.PreVote, validators);

    public MajorityBox PreCommitMajorities { get; } = new(height, index, VoteType.PreCommit, validators);

    public bool IsQuorumReached { get; set; }

    public bool IsPreVoteTimeoutScheduled { get; private set; }

    public bool IsPreCommitTimeoutScheduled { get; private set; }

    public bool IsPreCommitWaitScheduled { get; private  set; }

    public bool IsEndCommitWaitScheduled { get; private set; }

    public bool TrySetPreVoteTimeout()
    {
        if (IsPreVoteTimeoutScheduled)
        {
            return false;
        }

        IsPreVoteTimeoutScheduled = true;
        return true;
    }

    public bool TrySetPreCommitTimeout()
    {
        if (IsPreCommitTimeoutScheduled)
        {
            return false;
        }

        IsPreCommitTimeoutScheduled = true;
        return true;
    }

    public bool TrySetPreCommitWait()
    {
        if (IsPreCommitWaitScheduled)
        {
            return false;
        }

        IsPreCommitWaitScheduled = true;
        return true;
    }

    public bool TrySetEndCommitWait()
    {
        if (IsEndCommitWaitScheduled)
        {
            return false;
        }

        IsEndCommitWaitScheduled = true;
        return true;
    }

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
