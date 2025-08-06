using Libplanet.Types;

namespace Libplanet.Net.Consensus;

internal sealed class ConsensusRound(int round, Consensus consensus)
{
    public int Height => consensus.Height;

    public int Round { get; } = ValidateRound(round);

    public ImmutableSortedSet<Validator> Validators => consensus.Validators;

    public VoteCollection PreVotes { get; } = new(consensus.Height, round, VoteType.PreVote, consensus.Validators);

    public VoteCollection PreCommits { get; } = new(consensus.Height, round, VoteType.PreCommit, consensus.Validators);

    public ConsensusStep Step { get; set; }

    public void Add(Vote vote)
    {
        if (vote.Height != Height)
        {
            var message = $"Vote height {vote.Height} does not match expected height {Height}";
            throw new ArgumentException(message, nameof(vote));
        }

        if (vote.Type != VoteType.PreVote && vote.Type != VoteType.PreCommit)
        {
            var message = $"Vote type {vote.Type} does not match expected type " +
                          $"{VoteType.PreVote} or {VoteType.PreCommit}";
            throw new ArgumentException(message, nameof(vote));
        }

        var validator = vote.Validator;
        if (!Validators.Contains(validator))
        {
            var message = $"Validator {validator} is not in the validators for height {Height}";
            throw new ArgumentException(message, nameof(vote));
        }

        if (Validators.GetValidator(validator).Power != vote.ValidatorPower)
        {
            var message = $"Validator {validator} power {vote.ValidatorPower} does not match " +
                          $"expected power {Validators.GetValidator(validator).Power}";
            throw new ArgumentException(message, nameof(vote));
        }

        if (vote.Type == VoteType.PreVote)
        {
            PreVotes.Add(vote);
        }
        else
        {
            PreCommits.Add(vote);
        }
    }

    public bool SetMaj23(Maj23 maj23)
    {
        if (maj23.Height != Height)
        {
            var message = $"Maj23 height {maj23.Height} does not match expected height {Height}";
            throw new ArgumentException(message, nameof(maj23));
        }

        if (maj23.VoteType != VoteType.PreVote && maj23.VoteType != VoteType.PreCommit)
        {
            var message = $"Maj23 vote type {maj23.VoteType} does not match expected type " +
                          $"{VoteType.PreVote} or {VoteType.PreCommit}";
            throw new ArgumentException(message, nameof(maj23));
        }

        var validator = maj23.Validator;
        if (!Validators.Contains(validator))
        {
            var message = $"Validator {validator} is not in the validators for height {Height}";
            throw new ArgumentException(message, nameof(maj23));
        }

        if (maj23.VoteType == VoteType.PreVote)
        {
            return PreVotes.SetMaj23(maj23);
        }
        else
        {
            return PreCommits.SetMaj23(maj23);
        }
    }

    private static int ValidateRound(int round)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(round, 0);
        return round;
    }
}
