namespace Libplanet.Types;

public static class ImmutableSortedSetExtensions
{
    public static Validator GetProposer(this ImmutableSortedSet<Validator> @this, int height, int round)
    {
        if (@this.IsEmpty)
        {
            throw new ArgumentException($"Given {nameof(@this)} should not be empty.", nameof(@this));
        }

        return @this[(height + round) % @this.Count];
    }

    public static Validator GetValidator(
        this ImmutableSortedSet<Validator> @this, Address address)
    {
        return @this.First(item => item.Address == address);
    }

    public static bool Contains(
        this ImmutableSortedSet<Validator> @this, Address address)
    {
        return @this.Any(item => item.Address == address);
    }

    public static BigInteger GetTotalPower(this ImmutableSortedSet<Validator> @this)
        => @this.Aggregate(BigInteger.Zero, (acc, validator) => acc + validator.Power);

    public static BigInteger GetOneThirdPower(this ImmutableSortedSet<Validator> @this) => GetTotalPower(@this) / 3;

    public static BigInteger GetTwoThirdsPower(this ImmutableSortedSet<Validator> @this)
        => GetTotalPower(@this) * 2 / 3;

    public static BigInteger GetValidatorsPower(
        this ImmutableSortedSet<Validator> @this, IEnumerable<Address> addresss)
    {
        return addresss.Select(item => GetValidator(@this, item)).Aggregate(
            BigInteger.Zero, (total, next) => total + next.Power);
    }

    public static void ValidateBlockCommitValidators(
        this ImmutableSortedSet<Validator> @this, BlockCommit blockCommit)
    {
        if (!@this.Select(validator => validator.Address)
                .SequenceEqual(
                    blockCommit.Votes.Select(vote => vote.Validator).ToList()))
        {
            var message = $"BlockCommit of BlockHash {blockCommit.BlockHash} " +
                $"has different validator set with chain state's validator set: \n" +
                $"in states | \n " +
                @this.Aggregate(
                    string.Empty, (s, v) => s + v.Address + ", \n") +
                $"in blockCommit | \n " +
                blockCommit.Votes.Aggregate(
                    string.Empty, (s, v) => s + v.Validator + ", \n");
            throw new ArgumentException(message, nameof(blockCommit));
        }

        if (!blockCommit.Votes.All(
            v => v.ValidatorPower == GetValidator(@this, v.Validator).Power))
        {
            var message = $"BlockCommit of BlockHash {blockCommit.BlockHash} " +
                $"has different validator power with chain state's validator set: \n" +
                $"in states | \n " +
                @this.Aggregate(
                    string.Empty, (s, v) => s + v.Power + ", \n") +
                $"in blockCommit | \n " +
                blockCommit.Votes.Aggregate(
                    string.Empty, (s, v) => s + v.ValidatorPower + ", \n");
            throw new ArgumentException(message, nameof(blockCommit));
        }
    }

    // public static void ValidateLegacyBlockCommitValidators(
    //     this ImmutableSortedSet<Validator> @this, BlockCommit blockCommit)
    // {
    //     if (!@this.Select(validator => validator.Address).SequenceEqual(
    //         blockCommit.Votes.Select(vote => vote.Validator).ToList()))
    //     {
    //         throw new InvalidOperationException(
    //             $"BlockCommit of BlockHash {blockCommit.BlockHash} " +
    //             $"has different validator set with chain state's validator set: \n" +
    //             $"in states | \n " +
    //             @this.Aggregate(
    //                 string.Empty, (s, key) => s + key + ", \n") +
    //             $"in blockCommit | \n " +
    //             blockCommit.Votes.Aggregate(
    //                 string.Empty, (s, key) => s + key.Validator + ", \n"));
    //     }
    // }
}
