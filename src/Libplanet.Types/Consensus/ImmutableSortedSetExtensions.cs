
using Libplanet.Crypto;

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
}
