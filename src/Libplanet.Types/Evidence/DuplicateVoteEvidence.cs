using Libplanet.Serialization;
using Libplanet.Types.Consensus;
using ValidationContext = System.ComponentModel.DataAnnotations.ValidationContext;
using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace Libplanet.Types.Evidence;

[Model(Version = 1)]
public sealed partial record class DuplicateVoteEvidence : EvidenceBase, IEquatable<DuplicateVoteEvidence>
{
    [Property(0)]
    public required Vote VoteRef { get; init; }

    [Property(1)]
    public required Vote VoteDup { get; init; }

    [Property(2)]
    public BigInteger ValidatorPower { get; init; }

    [Property(3)]
    public BigInteger TotalPower { get; init; }

    public static DuplicateVoteEvidence Create(
        Vote voteRef, Vote voteDup, ImmutableSortedSet<Validator> validators) => new()
        {
            Height = voteRef.Height,
            TargetAddress = voteRef.Validator,
            VoteRef = voteRef,
            VoteDup = voteDup,
            ValidatorPower = validators.GetValidator(voteRef.Validator).Power,
            TotalPower = validators.GetTotalPower(),
            Timestamp = voteDup.Timestamp,
        };

    public static int Compare(Vote voteRef, Vote voteDup)
    {
        var result = voteRef.Timestamp.CompareTo(voteDup.Timestamp);
        if (result == 0)
        {
            result = voteRef.BlockHash.CompareTo(voteDup.BlockHash);
        }

        return result;
    }

    protected override IEnumerable<ValidationResult> OnValidate(ValidationContext validationContext)
    {
        foreach (var item in base.OnValidate(validationContext))
        {
            yield return item;
        }

        if (validationContext.Items.TryGetValue(typeof(EvidenceContext), out var value)
            && value is EvidenceContext evidenceContext)
        {
            if (!evidenceContext.Validators.Contains(VoteRef.Validator))
            {
                yield return new ValidationResult(
                    $"Validator {VoteRef.Validator} is not registered");
            }
        }
        else
        {
            yield return new ValidationResult(
                $"{nameof(EvidenceContext)} is not available: {validationContext}");
        }

        if (VoteRef.Height != Height)
        {
            yield return new ValidationResult(
                $"Height of voteRef is different from height: " +
                $"Expected {Height}, Actual {VoteRef.Height}");
        }

        if (VoteDup.Height != Height)
        {
            yield return new ValidationResult(
                $"Height of voteDup is different from height: " +
                $"Expected {Height}, Actual {VoteDup.Height}");
        }

        if (VoteRef.Round != VoteDup.Round)
        {
            yield return new ValidationResult(
                $"Round of votes are different: " +
                $"voteRef {VoteRef.Round}, voteDup {VoteDup.Round}");
        }

        if (VoteRef.Validator != VoteDup.Validator)
        {
            yield return new ValidationResult(
                $"Validator public key of votes are different: " +
                $"voteRef {VoteRef.Validator}, " +
                $"voteDup {VoteDup.Validator}");
        }

        if (VoteRef.Flag != VoteDup.Flag)
        {
            yield return new ValidationResult(
                $"Flags of votes are different: " +
                $"voteRef {VoteRef.Flag}, voteDup {VoteDup.Flag}");
        }

        if (VoteRef.BlockHash == default)
        {
            yield return new ValidationResult($"voteRef is nil vote");
        }

        if (VoteDup.BlockHash == default)
        {
            yield return new ValidationResult($"voteDup is nil vote");
        }

        if (VoteRef.BlockHash == VoteDup.BlockHash)
        {
            yield return new ValidationResult(
                $"Blockhash of votes are the same: {VoteDup}");
        }

        if (!ValidationUtility.TryValidate(VoteRef))
        {
            yield return new ValidationResult(
                $"Signature of voteRef is invalid: " +
                $"voteRef {VoteRef}, " +
                $"signature {VoteRef.Signature}");
        }

        if (!ValidationUtility.TryValidate(VoteDup))
        {
            yield return new ValidationResult(
                $"Signature of voteDup is invalid: " +
                $"voteDup {VoteDup}, " +
                $"signature {VoteDup.Signature}");
        }

        if (ValidatorPower <= BigInteger.Zero)
        {
            yield return new ValidationResult($"Validator Power is not positive");
        }

        if (TotalPower <= BigInteger.Zero)
        {
            yield return new ValidationResult($"Total power is not positive");
        }
    }
}
