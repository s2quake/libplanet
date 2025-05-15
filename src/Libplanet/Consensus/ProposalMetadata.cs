using System.Globalization;
using System.Text.Json.Serialization;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Consensus;

public class ProposalMetadata : IEquatable<ProposalMetadata>
{
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";

    public ProposalMetadata(
        int height,
        int round,
        DateTimeOffset timestamp,
        PublicKey validatorPublicKey,
        byte[] marshaledBlock,
        int validRound)
    {
        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                "Height must be greater than or equal to 0.");
        }
        else if (round < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(round),
                "Round must be greater than or equal to 0.");
        }
        else if (validRound < -1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(validRound),
                "ValidRound must be greater than or equal to -1.");
        }

        Height = height;
        Round = round;
        BlockHash = ModelSerializer.DeserializeFromBytes<BlockHash>(marshaledBlock);
        Timestamp = timestamp;
        ValidatorPublicKey = validatorPublicKey;
        MarshaledBlock = marshaledBlock;
        ValidRound = validRound;
    }

    /// <summary>
    /// A height of given proposal values.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// A round of given proposal values.
    /// </summary>
    public int Round { get; }

    /// <summary>
    /// The <see cref="Types.Blocks.BlockHash"/> of <see cref="MarshaledBlock"/>.
    /// This is automatically derived from <see cref="MarshaledBlock"/>.
    /// </summary>
    public BlockHash BlockHash { get; }

    /// <summary>
    /// The time at which the proposal took place.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// A <see cref="PublicKey"/> of proposing validator.
    /// </summary>
    public PublicKey ValidatorPublicKey { get; }

    /// <summary>
    /// A marshaled bencodex-encoded <see cref="byte"/> array of block.
    /// </summary>
    public byte[] MarshaledBlock { get; }

    /// <summary>
    /// a latest valid round at the moment of given proposal.
    /// </summary>
    public int ValidRound { get; }

    /// <summary>
    /// Signs given <see cref="ProposalMetadata"/> with given <paramref name="signer"/>.
    /// </summary>
    /// <param name="signer">A <see cref="PrivateKey"/> to sign.</param>
    /// <returns>Returns a signed <see cref="Proposal"/>.</returns>
    public Proposal Sign(PrivateKey signer) =>
        new Proposal(this, signer.Sign(ModelSerializer.SerializeToBytes(this)).ToImmutableArray());

    public bool Equals(ProposalMetadata? other)
    {
        return other is ProposalMetadata metadata &&
            Height == metadata.Height &&
            Round == metadata.Round &&
            BlockHash.Equals(metadata.BlockHash) &&
            Timestamp
                .ToString(TimestampFormat, CultureInfo.InvariantCulture).Equals(
                    metadata.Timestamp.ToString(
                        TimestampFormat,
                        CultureInfo.InvariantCulture)) &&
            ValidatorPublicKey.Equals(metadata.ValidatorPublicKey) &&
            ValidRound == metadata.ValidRound;
    }

    public override bool Equals(object? obj) =>
    obj is ProposalMetadata other && Equals(other);

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Height,
            Round,
            BlockHash,
            Timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture),
            ValidatorPublicKey,
            ValidRound);
    }
}
