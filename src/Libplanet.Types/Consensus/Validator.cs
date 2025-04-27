using System.Text.Json.Serialization;
using Libplanet.Crypto;
using Libplanet.Serialization;

namespace Libplanet.Types.Consensus;

[Model(Version = 1)]
public sealed record class Validator(PublicKey PublicKey, BigInteger Power)
    : IComparable<Validator>, IComparable
{
    [Property(0)]
    public PublicKey PublicKey { get; } = PublicKey;

    [Property(1)]
    public BigInteger Power { get; } = ValidatePower(Power);

    [JsonIgnore]
    public Address OperatorAddress => PublicKey.Address;

    public override string ToString() => $"{PublicKey}:{Power}";

    public int CompareTo(object? obj)
    {
        if (obj is Validator other)
        {
            return CompareTo(other);
        }

        return 1;
    }

    public int CompareTo(Validator? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = Power.CompareTo(other.Power);
        if (result == 0)
        {
            result = OperatorAddress.CompareTo(other.OperatorAddress);
        }

        return result;
    }

    private static BigInteger ValidatePower(BigInteger power)
    {
        if (power < BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(power),
                $"Given {nameof(power)} cannot be negative: {power}");
        }

        return power;
    }
}
