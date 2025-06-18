using Libplanet.Types;
using Serilog;

namespace Libplanet.Net.Consensus;

public class HeightVoteSet
{
    private readonly object _lock;
    private int _height;
    private ImmutableSortedSet<Validator> _validatorSet;
    private Dictionary<int, RoundVoteSet> _roundVoteSets;
    private int _round;

    public HeightVoteSet(int height, ImmutableSortedSet<Validator> validatorSet)
    {
        _lock = new object();
        lock (_lock)
        {
            _height = height;
            _validatorSet = validatorSet;
            _roundVoteSets = new Dictionary<int, RoundVoteSet>();
        }

        Reset(height, validatorSet);
    }

    public int Count => _roundVoteSets.Values.Sum(v => v.Count);

    public void Reset(int height, ImmutableSortedSet<Validator> validatorSet)
    {
        lock (_lock)
        {
            _height = height;
            _validatorSet = validatorSet;
            _roundVoteSets = new Dictionary<int, RoundVoteSet>();

            AddRound(0);
            _round = 0;
        }
    }

    public int Height()
    {
        lock (_lock)
        {
            return _height;
        }
    }

    public int Round()
    {
        lock (_lock)
        {
            return _round;
        }
    }

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

        VoteSet preVotes = new VoteSet(_height, round, VoteFlag.PreVote, _validatorSet);
        VoteSet preCommits = new VoteSet(_height, round, VoteFlag.PreCommit, _validatorSet);
        _roundVoteSets[round] = new RoundVoteSet(preVotes, preCommits);
    }

    // Duplicate votes return added=false, err=nil.
    // By convention, peerID is "" if origin is self.
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

            if (!_validatorSet.Contains(validatorKey))
            {
                throw new InvalidVoteException(
                    "ValidatorKey of the vote is not in the validator set",
                    vote);
            }

            if (_validatorSet.GetValidator(validatorKey).Power != vote.ValidatorPower)
            {
                const string msg = "ValidatorPower of the vote is given and the value is " +
                                   "not the same with the one in the validator set";
                throw new InvalidVoteException(msg, vote);
            }

            if (!vote.Flag.Equals(VoteFlag.PreVote) &&
                !vote.Flag.Equals(VoteFlag.PreCommit))
            {
                throw new InvalidVoteException(
                    $"VoteFlag should be either {VoteFlag.PreVote} or {VoteFlag.PreCommit}",
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
            return GetVoteSet(round, VoteFlag.PreVote);
        }
    }

    public VoteSet PreCommits(int round)
    {
        lock (_lock)
        {
            return GetVoteSet(round, VoteFlag.PreCommit);
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
                    VoteSet voteSet = GetVoteSet(r, VoteFlag.PreVote);
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

    public VoteSet GetVoteSet(int round, VoteFlag voteFlag)
    {
        RoundVoteSet roundVoteSet = _roundVoteSets[round];
        return voteFlag switch
        {
            VoteFlag.PreVote => roundVoteSet.PreVotes,
            VoteFlag.PreCommit => roundVoteSet.PreCommits,
            _ => throw new ArgumentException($"Unexpected vote type: {voteFlag}"),
        };
    }

    public bool SetPeerMaj23(Maj23 maj23)
    {
        lock (_lock)
        {
            if (!maj23.VoteFlag.Equals(VoteFlag.PreVote) &&
                !maj23.VoteFlag.Equals(VoteFlag.PreCommit))
            {
                throw new InvalidMaj23Exception(
                    $"Maj23 must have either {VoteFlag.PreVote} or {VoteFlag.PreCommit} " +
                    $"(Actual: {maj23.VoteFlag})",
                    maj23);
            }

            VoteSet voteSet;
            try
            {
                voteSet = GetVoteSet(maj23.Round, maj23.VoteFlag);
            }
            catch (KeyNotFoundException)
            {
                AddRound(maj23.Round);
                voteSet = GetVoteSet(maj23.Round, maj23.VoteFlag);
            }

            return voteSet.SetPeerMaj23(maj23);
        }
    }

    private sealed class RoundVoteSet(VoteSet preVotes, VoteSet preCommits)
    {
        public VoteSet PreVotes { get; set; } = preVotes;

        public VoteSet PreCommits { get; set; } = preCommits;

        public int Count => PreVotes.TotalCount + PreCommits.TotalCount;
    }
}
