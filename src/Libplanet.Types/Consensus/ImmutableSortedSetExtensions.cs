
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Consensus;

public static class ImmutableSortedSetExtensions
{
    public static Validator GetProposer(
        this ImmutableSortedSet<Validator> @this, long height, int round)
    {
        if (@this.IsEmpty)
        {
            throw new ArgumentException(
                $"Given {nameof(@this)} should not be empty.",
                nameof(@this));
        }

        return @this[(int)((height + round) % @this.Count)];
    }

    public static Validator GetValidator(
        this ImmutableSortedSet<Validator> @this, PublicKey publicKey)
    {
        return @this.First(item => item.PublicKey == publicKey);
    }

    public static bool Contains(
        this ImmutableSortedSet<Validator> @this, PublicKey publicKey)
    {
        return @this.Any(item => item.PublicKey == publicKey);
    }

    public static BigInteger GetTotalPower(this ImmutableSortedSet<Validator> @this)
        => @this.Aggregate(BigInteger.Zero, (acc, validator) => acc + validator.Power);

    public static BigInteger GetTwoThirdsPower(this ImmutableSortedSet<Validator> @this)
        => GetTotalPower(@this) * 2 / 3;

    public static void ValidateBlockCommitValidators(
        this ImmutableSortedSet<Validator> @this, BlockCommit blockCommit)
    {
        if (!@this.Select(validator => validator.PublicKey)
                .SequenceEqual(
                    blockCommit.Votes.Select(vote => vote.ValidatorPublicKey).ToList()))
        {
            throw new InvalidOperationException(
                $"BlockCommit of BlockHash {blockCommit.BlockHash} " +
                $"has different validator set with chain state's validator set: \n" +
                $"in states | \n " +
                @this.Aggregate(
                    string.Empty, (s, v) => s + v.PublicKey + ", \n") +
                $"in blockCommit | \n " +
                blockCommit.Votes.Aggregate(
                    string.Empty, (s, v) => s + v.ValidatorPublicKey + ", \n"));
        }

        if (!blockCommit.Votes.All(
            v => v.ValidatorPower == GetValidator(@this, v.ValidatorPublicKey).Power))
        {
            throw new InvalidOperationException(
                $"BlockCommit of BlockHash {blockCommit.BlockHash} " +
                $"has different validator power with chain state's validator set: \n" +
                $"in states | \n " +
                @this.Aggregate(
                    string.Empty, (s, v) => s + v.Power + ", \n") +
                $"in blockCommit | \n " +
                blockCommit.Votes.Aggregate(
                    string.Empty, (s, v) => s + v.ValidatorPower + ", \n"));
        }
    }

    public static void ValidateLegacyBlockCommitValidators(
        this ImmutableSortedSet<Validator> @this, BlockCommit blockCommit)
    {
        // if (blockCommit.Votes.Any(v => v.ValidatorPower is not null))
        // {
        //     throw new InvalidBlockCommitException(
        //         "All votes in the block commit before block protocol version 10 " +
        //         "must have null power.");
        // }

        if (!@this.Select(validator => validator.PublicKey).SequenceEqual(
            blockCommit.Votes.Select(vote => vote.ValidatorPublicKey).ToList()))
        {
            throw new InvalidOperationException(
                $"BlockCommit of BlockHash {blockCommit.BlockHash} " +
                $"has different validator set with chain state's validator set: \n" +
                $"in states | \n " +
                @this.Aggregate(
                    string.Empty, (s, key) => s + key + ", \n") +
                $"in blockCommit | \n " +
                blockCommit.Votes.Aggregate(
                    string.Empty, (s, key) => s + key.ValidatorPublicKey + ", \n"));
        }
    }
}
