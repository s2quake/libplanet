// using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using Bencodex.Misc;
using Bencodex.Types;
using Libplanet.Serialization;
using Libplanet.Types.Consensus;

namespace Libplanet.Types.Evidence;

[Model(Version = 1)]
public sealed record class DuplicateVoteEvidence
    : EvidenceBase, IEquatable<DuplicateVoteEvidence>
{
    private static readonly byte[] VoteRefKey = { 0x76 };             // 'v'
    private static readonly byte[] VoteDupKey = { 0x56 };             // 'V'
    private static readonly byte[] ValidatorPowerKey = { 0x70 };      // 'p'
    private static readonly byte[] TotalPowerKey = { 0x50 };          // 'P'

    /// <summary>
    /// Creates a <see cref="DuplicateVoteEvidence"/> instance.
    /// </summary>
    /// <param name="voteRef">Reference vote of conflicting <see cref="Vote"/>s.</param>
    /// <param name="voteDup">Duplicated vote of conflicting <see cref="Vote"/>s.</param>
    /// <param name="validators"><see cref="ImmutableSortedSet<Validator>"/>
    /// from block of conflicting votes has been made.</param>
    /// <param name="timestamp">The timestamp of evidence.</param>
    public static DuplicateVoteEvidence Create(
        Vote voteRef,
        Vote voteDup,
        ImmutableSortedSet<Validator> validators,
        DateTimeOffset timestamp)
        // : this(
        //       voteRef.Height,
        //       voteRef,
        //       voteDup,
        //       validators.GetValidator(voteRef.ValidatorPublicKey).Power,
        //       validators.GetTotalPower(),
        //       timestamp)
    {
        throw new NotImplementedException();
    //     return DuplicateVoteEvidence.Create
    //     {

    //     }
    }

    // /// <summary>
    // /// Creates a <see cref="DuplicateVoteEvidence"/> instance from
    // /// bencoded <see cref="IValue"/>.
    // /// </summary>
    // /// <param name="bencoded">Bencoded <see cref="IValue"/>.</param>
    // public DuplicateVoteEvidence(IValue bencoded)
    //     : base(bencoded)
    // {
    //     if (bencoded is Dictionary dictionary)
    //     {
    //         VoteRef = new Vote(dictionary.GetValue<IValue>(VoteRefKey));
    //         VoteDup = new Vote(dictionary.GetValue<IValue>(VoteDupKey));
    //         ValidatorPower = dictionary.GetValue<Integer>(ValidatorPowerKey);
    //         TotalPower = dictionary.GetValue<Integer>(TotalPowerKey);
    //     }
    //     else
    //     {
    //         throw new ArgumentException(
    //             "Given bencoded must be of type Dictionary.", nameof(bencoded));
    //     }
    // }

    // private DuplicateVoteEvidence(
    //     long height,
    //     Vote voteRef,
    //     Vote voteDup,
    //     BigInteger validatorPower,
    //     BigInteger totalPower,
    //     DateTimeOffset timestamp)
    //     : base(height, voteRef.ValidatorPublicKey.Address, timestamp)
    // {
    //     if (voteRef.Height != height)
    //     {
    //         throw new ArgumentException(
    //             $"Height of voteRef is different from height : " +
    //             $"Expected {height}, Actual {voteRef.Height}");
    //     }

    //     if (voteDup.Height != height)
    //     {
    //         throw new ArgumentException(
    //             $"Height of voteDup is different from height : " +
    //             $"Expected {height}, Actual {voteDup.Height}");
    //     }

    //     if (voteRef.Round != voteDup.Round)
    //     {
    //         throw new ArgumentException(
    //             $"Round of votes are different : " +
    //             $"voteRef {voteRef.Round}, voteDup {voteDup.Round}");
    //     }

    //     if (voteRef.ValidatorPublicKey != voteDup.ValidatorPublicKey)
    //     {
    //         throw new ArgumentException(
    //             $"Validator public key of votes are different : " +
    //             $"voteRef {voteRef.ValidatorPublicKey}," +
    //             $"voteDup {voteDup.ValidatorPublicKey}");
    //     }

    //     if (voteRef.Flag != voteDup.Flag)
    //     {
    //         throw new ArgumentException(
    //             $"Flags of votes are different : " +
    //             $"voteRef {voteRef.Flag}, voteDup {voteDup.Flag}");
    //     }

    //     if (voteRef.BlockHash is { } voteRefHash)
    //     {
    //     }
    //     else
    //     {
    //         throw new ArgumentException(
    //             $"voteRef is nill vote");
    //     }

    //     if (voteDup.BlockHash is { } voteDupHash)
    //     {
    //     }
    //     else
    //     {
    //         throw new ArgumentException(
    //             $"voteDup is nill vote");
    //     }

    //     if (voteRefHash.Equals(voteDupHash))
    //     {
    //         throw new ArgumentException(
    //             $"Blockhash of votes are same : " +
    //             $"{voteRefHash}");
    //     }

    //     if (!voteRef.Verify())
    //     {
    //         throw new ArgumentException(
    //             $"Signature of voteRef is invalid : " +
    //             $"voteRef {voteRef}," +
    //             $"signature {voteRef.Signature.Hex()}");
    //     }

    //     if (!voteDup.Verify())
    //     {
    //         throw new ArgumentException(
    //             $"Signature of voteDup is invalid : " +
    //             $"voteDup {voteDup}," +
    //             $"signature {voteDup.Signature.Hex()}");
    //     }

    //     if (height < 0L)
    //     {
    //         throw new ArgumentException(
    //             $"Height is not positive");
    //     }

    //     if (validatorPower <= BigInteger.Zero)
    //     {
    //         throw new ArgumentException(
    //             $"Validator Power is not positive");
    //     }

    //     if (totalPower <= BigInteger.Zero)
    //     {
    //         throw new ArgumentException(
    //             $"Total power is not positive");
    //     }

    //     (VoteRef, VoteDup) = OrderDuplicateVotePair(voteRef, voteDup);
    //     ValidatorPower = validatorPower;
    //     TotalPower = totalPower;
    // }

    public required Vote VoteRef { get; init; }

    public required Vote VoteDup { get; init; }

    public BigInteger ValidatorPower { get; init; }

    public BigInteger TotalPower { get; init; }

    public static (Vote, Vote) OrderDuplicateVotePair(Vote voteRef, Vote voteDup)
    {
        if (voteRef.BlockHash is { } voteRefHash)
        {
        }
        else
        {
            throw new ArgumentException(
                $"voteRef is nill vote");
        }

        if (voteDup.BlockHash is { } voteDupHash)
        {
        }
        else
        {
            throw new ArgumentException(
                $"voteDup is nill vote");
        }

        if (voteRef.Timestamp < voteDup.Timestamp)
        {
            return (voteRef, voteDup);
        }
        else if (voteRef.Timestamp > voteDup.Timestamp)
        {
            return (voteDup, voteRef);
        }
        else
        {
            if (voteRefHash.CompareTo(voteDupHash) < 0)
            {
                return (voteRef, voteDup);
            }
            else
            {
                return (voteDup, voteRef);
            }
        }
    }

    protected override void OnVerify(IEvidenceContext evidenceContext)
    {
        var validators = evidenceContext.Validators;

        if (!validators.Contains(VoteRef.ValidatorPublicKey))
        {
            throw new InvalidOperationException(
                $"Evidence public key is not a validator. " +
                $"PublicKey: {VoteRef.ValidatorPublicKey}");
        }

        BigInteger validatorPower
            = validators.GetValidator(VoteRef.ValidatorPublicKey).Power;
        BigInteger totalPower = validators.GetTotalPower();

        if (ValidatorPower != validatorPower)
        {
            throw new InvalidOperationException(
                $"Evidence validator power is different from the actual. " +
                $"Expected: {validatorPower}, " +
                $"Actual: {ValidatorPower}");
        }

        if (TotalPower != totalPower)
        {
            throw new InvalidOperationException(
                $"Evidence total power is different from the actual. " +
                $"Expected: {totalPower}, " +
                $"Actual: {TotalPower}");
        }
    }

    protected override IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> OnValidate(System.ComponentModel.DataAnnotations.ValidationContext validationContext)
    {
        foreach (var item in base.OnValidate(validationContext))
        {
            yield return item;
        }

        if (VoteRef.Height != Height)
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                $"Height of voteRef is different from height: " +
                $"Expected {Height}, Actual {VoteRef.Height}");
        }

        if (VoteDup.Height != Height)
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                $"Height of voteDup is different from height: " +
                $"Expected {Height}, Actual {VoteDup.Height}");
        }

        if (VoteRef.Round != VoteDup.Round)
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                $"Round of votes are different: " +
                $"voteRef {VoteRef.Round}, voteDup {VoteDup.Round}");
        }

        if (VoteRef.ValidatorPublicKey != VoteDup.ValidatorPublicKey)
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                $"Validator public key of votes are different: " +
                $"voteRef {VoteRef.ValidatorPublicKey}, " +
                $"voteDup {VoteDup.ValidatorPublicKey}");
        }

        if (VoteRef.Flag != VoteDup.Flag)
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                $"Flags of votes are different: " +
                $"voteRef {VoteRef.Flag}, voteDup {VoteDup.Flag}");
        }

        // if (VoteRef.BlockHash is not { } voteRefHash)
        // {
        //     yield return new ValidationResult($"voteRef is nil vote");
        // }

        // if (VoteDup.BlockHash is not { } voteDupHash)
        // {
        //     yield return new ValidationResult($"voteDup is nil vote");
        // }

        // if (voteRefHash?.Equals(voteDupHash) == true)
        // {
        //     yield return new ValidationResult(
        //         $"Blockhash of votes are the same: {voteRefHash}");
        // }

        if (!VoteRef.Verify())
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                $"Signature of voteRef is invalid: " +
                $"voteRef {VoteRef}, " +
                $"signature {VoteRef.Signature.Hex()}");
        }

        if (!VoteDup.Verify())
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                $"Signature of voteDup is invalid: " +
                $"voteDup {VoteDup}, " +
                $"signature {VoteDup.Signature.Hex()}");
        }

        if (Height < 0L)
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult($"Height is not positive");
        }

        if (ValidatorPower <= BigInteger.Zero)
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult($"Validator Power is not positive");
        }

        if (TotalPower <= BigInteger.Zero)
        {
            yield return new System.ComponentModel.DataAnnotations.ValidationResult($"Total power is not positive");
        }
    }
}
