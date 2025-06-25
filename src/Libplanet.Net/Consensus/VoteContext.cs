using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class VoteContext(int height, ImmutableSortedSet<Validator> validators)
{
    private readonly Dictionary<int, VoteCollection> _preVotesByRound = [];
    private readonly Dictionary<int, VoteCollection> _preCommitsByRound = [];

    private int _round;

    public int Height { get; } = height;

    public int Round
    {
        get => _round;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Round must be non-negative");
            }

            for (var i = _round + 1; i <= value; i++)
            {
                if (!_preVotesByRound.ContainsKey(i))
                {
                    _preVotesByRound[i] = new VoteCollection(Height, i, VoteType.PreVote, validators);
                }

                if (!_preCommitsByRound.ContainsKey(i))
                {
                    _preCommitsByRound[i] = new VoteCollection(Height, i, VoteType.PreCommit, validators);
                }
            }

            _round = value;
        }
    }

    public void AddVote(Vote vote)
    {
        if (vote.Height != Height)
        {
            var message = $"Vote height {vote.Height} does not match HeightVoteSet height {Height}";
            throw new ArgumentException(message, nameof(vote));
        }

        var validator = vote.Validator;
        if (!validators.Contains(validator))
        {
            var message = $"Validator {validator} is not in the validator set for height {Height}";
            throw new ArgumentException(message, nameof(vote));
        }

        if (validators.GetValidator(validator).Power != vote.ValidatorPower)
        {
            var message = $"Validator {validator} power {vote.ValidatorPower} does not match " +
                          $"expected power {validators.GetValidator(validator).Power}";
            throw new ArgumentException(message, nameof(vote));
        }

        if (vote.Type is not VoteType.PreVote and not VoteType.PreCommit)
        {
            var message = $"Vote type must be either {VoteType.PreVote} or {VoteType.PreCommit} " +
                          $"(Actual: {vote.Type})";
            throw new ArgumentException(message, nameof(vote));
        }

        try
        {
            ValidationUtility.Validate(vote);
        }
        catch (Exception e)
        {
            throw new ArgumentException($"Received vote is invalid: {e.Message}", nameof(vote), e);
        }

        var round = vote.Round;
        var voteType = vote.Type;
        var dictionary = voteType == VoteType.PreVote ? _preVotesByRound : _preCommitsByRound;
        if (!dictionary.TryGetValue(round, out var votes))
        {
            dictionary[round] = votes = new VoteCollection(Height, round, voteType, validators);
        }

        votes.Add(vote);
    }

    public VoteCollection PreVotes(int round) => GetVotes(round, VoteType.PreVote);

    public VoteCollection PreCommits(int round) => GetVotes(round, VoteType.PreCommit);

    // Last round and block hash that has +2/3 prevotes for a particular block or nil.
    // Returns -1 if no such round exists.
    public (int, BlockHash) POLInfo()
    {
        for (var i = Round; i >= 0; i--)
        {
            try
            {
                var votes = GetVotes(i, VoteType.PreVote);
                var exists = votes.TwoThirdsMajority(out BlockHash polBlockHash);
                if (exists)
                {
                    return (i, polBlockHash);
                }
            }
            catch (KeyNotFoundException)
            {
                // do nothing
            }
        }

        return (-1, default);
    }

    public VoteCollection GetVotes(int round, VoteType voteType)
    {
        if (voteType is not VoteType.PreVote and not VoteType.PreCommit)
        {
            throw new ArgumentOutOfRangeException(nameof(voteType),
                $"Vote type must be either {VoteType.PreVote} or {VoteType.PreCommit} " +
                $"(Actual: {voteType})");
        }

        var dictionary = voteType == VoteType.PreVote ? _preVotesByRound : _preCommitsByRound;
        return dictionary[round];
    }

    public bool SetPeerMaj23(Maj23 maj23)
    {
        var round = maj23.Round;
        var voteType = maj23.VoteType;
        if (voteType is not VoteType.PreVote and not VoteType.PreCommit)
        {
            throw new InvalidMaj23Exception(
                $"Maj23 must have either {VoteType.PreVote} or {VoteType.PreCommit} " +
                $"(Actual: {voteType})",
                maj23);
        }

        var dictionary = voteType == VoteType.PreVote ? _preVotesByRound : _preCommitsByRound;
        if (!dictionary.TryGetValue(round, out var votes))
        {
            dictionary[round] = votes = new VoteCollection(Height, round, voteType, validators);
        }

        return votes.SetMaj23(maj23);
    }
}
