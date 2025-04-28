using System.Text.Json.Serialization;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Types.Consensus;

[Model(Version = 1)]
public sealed record class Validator : IComparable<Validator>, IComparable
{
    [Property(0)]
    public required PublicKey PublicKey { get; init; }

    [Property(1)]
    [Positive]
    public BigInteger Power { get; init; } = BigInteger.One;

    [JsonIgnore]
    public Address OperatorAddress => PublicKey.Address;

    public static Validator Create(PublicKey publicKey) => Create(publicKey, BigInteger.One);

    public static Validator Create(PublicKey publicKey, BigInteger power) => new()
    {
        PublicKey = publicKey,
        Power = power,
    };

    public override string ToString() => $"{PublicKey}:{Power}";

    public int CompareTo(object? obj) => obj is Validator other ? CompareTo(other) : 1;

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
}
