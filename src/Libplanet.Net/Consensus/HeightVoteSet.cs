using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class HeightVoteSet
{
    private readonly object _lock;
    private int _height;
    private ImmutableSortedSet<Validator> _validators;
    private Dictionary<int, RoundVoteSet> _roundVoteSets;
    private int _round;

    public HeightVoteSet(int height, ImmutableSortedSet<Validator> validators)
    {
        _lock = new object();
        _height = height;
        _validators = validators;
        _roundVoteSets = [];

        Reset(height, validators);
    }

    public int Count => _roundVoteSets.Values.Sum(v => v.Count);

    public void Reset(int height, ImmutableSortedSet<Validator> validators)
    {
        lock (_lock)
        {
            _height = height;
            _validators = validators;
            _roundVoteSets = new Dictionary<int, RoundVoteSet>();

            AddRound(0);
            _round = 0;
        }
    }

    public int Height => _height;

    public int Round => _round;

    // Create more RoundVoteSets up to round.
    public void SetRound(int round)
    {
        lock (_lock)
        {
            var newRound = _round + 1;
            if (_round != 0 && (round < newRound))
            {
                throw new ArgumentException("Round must increase");
            }

            for (int r = newRound; r <= round; r++)
            {
                if (_roundVoteSets.ContainsKey(r))
                {
                    continue; // Already exists because peerCatchupRounds.
                }

                AddRound(r);
            }

            _round = round;
        }
    }

    public void AddRound(int round)
    {
        if (_roundVoteSets.ContainsKey(round))
        {
            throw new ArgumentException($"Add round for an existing round: {round}");
        }

        VoteSet preVotes = new VoteSet(_height, round, VoteType.PreVote, _validators);
        VoteSet preCommits = new VoteSet(_height, round, VoteType.PreCommit, _validators);
        _roundVoteSets[round] = new RoundVoteSet(preVotes, preCommits);
    }

    public void AddVote(Vote vote)
    {
        lock (_lock)
        {
            if (vote.Height != _height)
            {
                throw new InvalidVoteException(
                    "Height of vote is different from current HeightVoteSet",
                    vote);
            }

            var validatorKey = vote.Validator;

            if (validatorKey == default)
            {
                throw new InvalidVoteException("ValidatorKey of the vote cannot be null", vote);
            }

            if (!_validators.Contains(validatorKey))
            {
                throw new InvalidVoteException(
                    "ValidatorKey of the vote is not in the validator set",
                    vote);
            }

            if (_validators.GetValidator(validatorKey).Power != vote.ValidatorPower)
            {
                const string msg = "ValidatorPower of the vote is given and the value is " +
                                   "not the same with the one in the validator set";
                throw new InvalidVoteException(msg, vote);
            }

            if (!vote.Flag.Equals(VoteType.PreVote) &&
                !vote.Flag.Equals(VoteType.PreCommit))
            {
                throw new InvalidVoteException(
                    $"VoteType should be either {VoteType.PreVote} or {VoteType.PreCommit}",
                    vote);
            }

            if (!ValidationUtility.TryValidate(vote))
            {
                throw new InvalidVoteException(
                    "Received vote's signature is invalid",
                    vote);
            }

            VoteSet voteSet;
            try
            {
                voteSet = GetVoteSet(vote.Round, vote.Flag);
            }
            catch (KeyNotFoundException)
            {
                AddRound(vote.Round);
                voteSet = GetVoteSet(vote.Round, vote.Flag);
            }

            voteSet.AddVote(vote);
        }
    }

    public VoteSet PreVotes(int round)
    {
        lock (_lock)
        {
            return GetVoteSet(round, VoteType.PreVote);
        }
    }

    public VoteSet PreCommits(int round)
    {
        lock (_lock)
        {
            return GetVoteSet(round, VoteType.PreCommit);
        }
    }

    // Last round and block hash that has +2/3 prevotes for a particular block or nil.
    // Returns -1 if no such round exists.
    public (int, BlockHash) POLInfo()
    {
        lock (_lock)
        {
            for (int r = _round; r >= 0; r--)
            {
                try
                {
                    VoteSet voteSet = GetVoteSet(r, VoteType.PreVote);
                    bool exists = voteSet.TwoThirdsMajority(out BlockHash polBlockHash);
                    if (exists)
                    {
                        return (r, polBlockHash);
                    }
                }
                catch (KeyNotFoundException)
                {
                    continue;
                }
            }

            return (-1, default);
        }
    }

    public VoteSet GetVoteSet(int round, VoteType voteType)
    {
        RoundVoteSet roundVoteSet = _roundVoteSets[round];
        return voteType switch
        {
            VoteType.PreVote => roundVoteSet.PreVotes,
            VoteType.PreCommit => roundVoteSet.PreCommits,
            _ => throw new ArgumentException($"Unexpected vote type: {voteType}"),
        };
    }

    public bool SetPeerMaj23(Maj23 maj23)
    {
        lock (_lock)
        {
            if (!maj23.VoteType.Equals(VoteType.PreVote) &&
                !maj23.VoteType.Equals(VoteType.PreCommit))
            {
                throw new InvalidMaj23Exception(
                    $"Maj23 must have either {VoteType.PreVote} or {VoteType.PreCommit} " +
                    $"(Actual: {maj23.VoteType})",
                    maj23);
            }

            VoteSet voteSet;
            try
            {
                voteSet = GetVoteSet(maj23.Round, maj23.VoteType);
            }
            catch (KeyNotFoundException)
            {
                AddRound(maj23.Round);
                voteSet = GetVoteSet(maj23.Round, maj23.VoteType);
            }

            return voteSet.SetPeerMaj23(maj23);
        }
    }
}
