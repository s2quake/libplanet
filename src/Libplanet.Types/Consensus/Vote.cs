using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Consensus;

[Model(Version = 1)]
public sealed record class Vote : IEquatable<Vote>
{
    [Property(0)]
    public required VoteMetadata Metadata { get; init; }

    [Property(1)]
    public ImmutableArray<byte> Signature { get; init; }

    public long Height => Metadata.Height;

    public int Round => Metadata.Round;

    public BlockHash BlockHash => Metadata.BlockHash;

    public DateTimeOffset Timestamp => Metadata.Timestamp;

    public PublicKey ValidatorPublicKey => Metadata.ValidatorPublicKey;

    public BigInteger ValidatorPower => Metadata.ValidatorPower;

    public VoteFlag Flag => Metadata.Flag;

    public bool Equals(Vote? other) => ModelUtility.Equals(this, other);

    public override int GetHashCode() => ModelUtility.GetHashCode(this);

    public bool Verify() => Metadata.Verify(Signature);

    private static ImmutableArray<byte> Validate(
        VoteMetadata metadata, ImmutableArray<byte> signature)
    {
        if (signature.IsDefault)
        {
            throw new ArgumentException(
                $"Given {nameof(signature)} should not be set to default; use " +
                $"an empty array to represent a lack of signature for a {nameof(Vote)}.",
                nameof(signature));
        }
        else if (!signature.IsEmpty)
        {
            if (metadata.Flag != VoteFlag.PreVote && metadata.Flag != VoteFlag.PreCommit)
            {
                throw new ArgumentException(
                    $"If {nameof(signature)} is not empty, {metadata.Flag} should be either " +
                    $"{VoteFlag.PreVote} or {VoteFlag.PreCommit}",
                    nameof(signature));
            }
            else if (!metadata.Verify(signature))
            {
                throw new ArgumentException(
                    $"Given {nameof(signature)} is invalid.",
                    nameof(signature));
            }
        }
        else if (signature.IsEmpty &&
            metadata.Flag != VoteFlag.Null && metadata.Flag != VoteFlag.Unknown)
        {
            throw new ArgumentException(
                $"If {nameof(signature)} is empty, {metadata.Flag} should be either " +
                $"{VoteFlag.Null} or {VoteFlag.Unknown}",
                nameof(signature));
        }

        return signature;
    }
}
