using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Crypto;

namespace Libplanet.Types.Consensus;

[Model(Version = 1)]
public sealed partial record class Validator : IComparable<Validator>, IComparable
{
    [Property(0)]
    public required Address Address { get; init; }

    [Property(1)]
    [Positive]
    public BigInteger Power { get; init; } = BigInteger.One;

    public override string ToString() => $"{Address}:{Power}";

    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        Validator other => CompareTo(other),
        _ => throw new ArgumentException($"Argument {nameof(obj)} is not ${nameof(Validator)}.", nameof(obj)),
    };

    public int CompareTo(Validator? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = Power.CompareTo(other.Power);
        if (result == 0)
        {
            result = Address.CompareTo(other.Address);
        }

        return result;
    }
}
