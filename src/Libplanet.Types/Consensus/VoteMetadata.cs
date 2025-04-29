using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Consensus;

[Model(Version = 1)]
public sealed record class VoteMetadata
{
    [Property(0)]
    public required PublicKey ValidatorPublicKey { get; init; }

    [Property(1)]
    public long Height { get; init; }

    [Property(2)]
    public int Round { get; init; }

    [Property(3)]
    public BlockHash BlockHash { get; init; }

    [Property(4)]
    public DateTimeOffset Timestamp { get; init; }

    [Property(5)]
    public BigInteger ValidatorPower { get; init; }

    [Property(6)]
    public VoteFlag Flag { get; init; }

    public void Verify()
    {
        if (Height < 0)
        {
            throw new ArgumentException(
                $"Given {nameof(Height)} cannot be negative: {Height}");
        }
        else if (Round < 0)
        {
            throw new ArgumentException(
                $"Given {nameof(Round)} cannot be negative: {Round}");
        }
        else if (ValidatorPower <= 0)
        {
            var msg = $"Given {nameof(ValidatorPower)} cannot be negative " +
                      $"or equal to zero: {ValidatorPower}";
            throw new ArgumentException(msg);
        }
        else if (
            BlockHash.Equals(default) && (Flag == VoteFlag.Null || Flag == VoteFlag.Unknown))
        {
            throw new ArgumentException(
                $"Given {nameof(BlockHash)} cannot be default if {nameof(Flag)} " +
                $"is {VoteFlag.Null} or {VoteFlag.Unknown}");
        }
    }

    public bool Verify(ImmutableArray<byte> signature)
    {
        return ValidatorPublicKey.Verify([.. ModelSerializer.SerializeToBytes(this)], signature);
    }

    public Vote Sign(PrivateKey signer)
    {
        var signature = signer.Sign(ModelSerializer.SerializeToBytes(this));
        return new Vote { Metadata = this, Signature = [.. signature] };
    }
}
