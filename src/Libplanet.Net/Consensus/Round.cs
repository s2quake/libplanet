using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class Round
{
    private readonly VoteCollection _preVotes;
    private readonly VoteCollection _preCommits;
    private readonly Maj23Collection _preVoteMaj23s;
    private readonly Maj23Collection _preCommitMaj23s;

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
        _preVotes = new(height, index, VoteType.PreVote, validators);
        _preCommits = new(height, index, VoteType.PreCommit, validators);
        _preVoteMaj23s = new(height, index, VoteType.PreVote, validators);
        _preCommitMaj23s = new(height, index, VoteType.PreCommit, validators);
    }

    public int Height { get; }

    public int Index { get; }

    public IVoteCollection PreVotes => _preVotes;

    public IVoteCollection PreCommits => _preCommits;

    public Maj23Collection PreVoteMaj23s => _preVoteMaj23s;

    public Maj23Collection PreCommitMaj23s => _preCommitMaj23s;

    public bool HasTwoThirdsPreVoteTypes { get; set; }

    public bool IsPreVoteTimeoutScheduled { get; private set; }

    public bool IsPreCommitTimeoutScheduled { get; private set; }

    public bool IsPreCommitWaitScheduled { get; private set; }

    public bool IsEndCommitWaitScheduled { get; private set; }

    private static void Vote(VoteCollection votes, Maj23Collection maj23s, Vote vote)
    {
        if (vote.Height != votes.Height)
        {
            throw new ArgumentException(
                $"Vote height {vote.Height} does not match expected height {votes.Height}", nameof(vote));
        }

        if (vote.Round != votes.Round)
        {
            throw new ArgumentException(
                $"Vote round {vote.Round} does not match expected round {votes.Round}", nameof(vote));
        }

        if (vote.Type != votes.Type)
        {
            throw new ArgumentException(
                $"Vote type {vote.Type} does not match expected type {votes.Type}", nameof(vote));
        }

        var validator = vote.Validator;
        var blockHash = vote.BlockHash;
        if (votes.TryGetValue(validator, out var existingVote))
        {
            if (existingVote.BlockHash == vote.BlockHash)
            {
                throw new ArgumentException("Vote already exists.", nameof(vote));
            }
            else if (votes.HasTwoThirdsMajority && votes.GetMajority23() == vote.BlockHash)
            {
                votes.Remove(validator);
            }
            else if (!maj23s.HasMaj23(blockHash))
            {
                throw new DuplicateVoteException("Duplicate PreVote detected.", existingVote, vote);
            }
            else
            {
                votes.Remove(validator);
            }
        }

        votes.Add(vote);
    }

    public void PreVote(Vote vote)
    {
        // if (vote.Height != Height || vote.Round != Index || vote.Type != VoteType.PreVote)
        // {
        //     throw new ArgumentException("Invalid vote for PreVote.", nameof(vote));
        // }

        // var validator = vote.Validator;
        // var blockHash = vote.BlockHash;
        // if (_preVotes.TryGetValue(validator, out var existingVote))
        // {
        //     if (existingVote.BlockHash == vote.BlockHash)
        //     {
        //         throw new ArgumentException("Vote already exists.", nameof(vote));
        //     }
        //     else if (_preVotes.HasTwoThirdsMajority && _preVotes.GetMajority23() == vote.BlockHash)
        //     {
        //         _preVotes.Remove(validator);
        //     }
        //     else if (!_preVoteMaj23s.HasMaj23(blockHash))
        //     {
        //         throw new DuplicateVoteException("Duplicate PreVote detected.", existingVote, vote);
        //     }
        //     else
        //     {
        //         _preVotes.Remove(validator);
        //     }
        // }

        // _preVotes.Add(vote);
        Vote(_preVotes, _preVoteMaj23s, vote);
    }

    public void PreCommit(Vote vote)
    {
        // if (vote.Height != Height || vote.Round != Index || vote.Type != VoteType.PreCommit)
        // {
        //     throw new ArgumentException("Invalid vote for PreCommit.", nameof(vote));
        // }

        // _preCommits.Add(vote);
        Vote(_preCommits, _preCommitMaj23s, vote);
    }

    public void PreVoteMaj23(Maj23 maj23)
    {
        if (maj23.Height != Height || maj23.Round != Index || maj23.VoteType != VoteType.PreVote)
        {
            throw new ArgumentException("Invalid Maj23 for PreVote.", nameof(maj23));
        }

        _preVoteMaj23s.Add(maj23);
    }

    public void PreCommitMaj23(Maj23 maj23)
    {
        if (maj23.Height != Height || maj23.Round != Index || maj23.VoteType != VoteType.PreCommit)
        {
            throw new ArgumentException("Invalid Maj23 for PreCommit.", nameof(maj23));
        }

        _preCommitMaj23s.Add(maj23);
    }

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
