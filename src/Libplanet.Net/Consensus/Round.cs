using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class Round
{
    public Round(int height, int index, ImmutableSortedSet<Validator> validators)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);

        if (validators.Count is 0)
        {
            throw new ArgumentException("Validators cannot be empty.", nameof(validators));
        }

        Height = height;
        Index = index;
        PreVotes = new(height, index, VoteType.PreVote, validators);
        PreCommits = new(height, index, VoteType.PreCommit, validators);
        PreVoteMaj23s = new(height, index, VoteType.PreVote, validators);
        PreCommitMaj23s = new(height, index, VoteType.PreCommit, validators);
    }

    public int Height { get; }

    public int Index { get; }

    public VoteCollection PreVotes { get; }

    public VoteCollection PreCommits { get; }

    public Maj23Collection PreVoteMaj23s { get; }

    public Maj23Collection PreCommitMaj23s { get; }

    public bool HasTwoThirdsPreVoteTypes { get; set; }

    public bool IsPreVoteTimeoutScheduled { get; private set; }

    public bool IsPreCommitTimeoutScheduled { get; private set; }

    public bool IsPreCommitWaitScheduled { get; private set; }

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
}
