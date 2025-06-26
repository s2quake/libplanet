using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public sealed class VoteContext(int height, VoteType voteType, ImmutableSortedSet<Validator> validators)
{
    private readonly Dictionary<int, VoteCollection> _votesByRound = [];

    private int _round;

    public int Height { get; } = ValidateHeight(height);

    public VoteType VoteType { get; } = ValidateVoteType(voteType);

    public int Round
    {
        get => _round;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Round must be non-negative");
            }

            _round = value;
        }
    }

    public VoteCollection this[int round]
    {
        get
        {
            if (round < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(round), "Round must be non-negative");
            }

            if (!_votesByRound.TryGetValue(round, out var votes))
            {
                votes = new VoteCollection(Height, round, voteType, validators);
                _votesByRound[round] = votes;
            }

            return votes;
        }
    }

    public void Add(Vote vote)
    {
        if (vote.Height != Height)
        {
            var message = $"Vote height {vote.Height} does not match expected height {Height}";
            throw new ArgumentException(message, nameof(vote));
        }

        if (vote.Type != voteType)
        {
            var message = $"Vote type {vote.Type} does not match expected type {voteType}";
            throw new ArgumentException(message, nameof(vote));
        }

        var validator = vote.Validator;
        if (!validators.Contains(validator))
        {
            var message = $"Validator {validator} is not in the validators for height {Height}";
            throw new ArgumentException(message, nameof(vote));
        }

        if (validators.GetValidator(validator).Power != vote.ValidatorPower)
        {
            var message = $"Validator {validator} power {vote.ValidatorPower} does not match " +
                          $"expected power {validators.GetValidator(validator).Power}";
            throw new ArgumentException(message, nameof(vote));
        }

        this[vote.Round].Add(vote);
    }

    public bool SetMaj23(Maj23 maj23)
    {
        if (maj23.Height != Height)
        {
            var message = $"Maj23 height {maj23.Height} does not match expected height {Height}";
            throw new ArgumentException(message, nameof(maj23));
        }

        if (maj23.VoteType != voteType)
        {
            var message = $"Maj23 vote type {maj23.VoteType} does not match expected type {voteType}";
            throw new ArgumentException(message, nameof(maj23));
        }

        var validator = maj23.Validator;
        if (!validators.Contains(validator))
        {
            var message = $"Validator {validator} is not in the validators for height {Height}";
            throw new ArgumentException(message, nameof(maj23));
        }

        return this[maj23.Round].SetMaj23(maj23);
    }

    private static int ValidateHeight(int height)
    {
        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be non-negative");
        }

        return height;
    }

    private static VoteType ValidateVoteType(VoteType voteType)
    {
        if (voteType is not VoteType.PreVote and not VoteType.PreCommit)
        {
            throw new ArgumentOutOfRangeException(nameof(voteType),
                $"Vote type must be either {VoteType.PreVote} or {VoteType.PreCommit} " +
                $"(Actual: {voteType})");
        }

        return voteType;
    }
}
