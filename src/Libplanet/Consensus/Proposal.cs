using System.Diagnostics.Contracts;
using System.Text.Json.Serialization;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;

namespace Libplanet.Consensus;

public class Proposal : IEquatable<Proposal>
{
    private readonly ProposalMetadata _proposalMetadata;

    public Proposal(ProposalMetadata proposalMetadata, ImmutableArray<byte> signature)
    {
        _proposalMetadata = proposalMetadata;
        Signature = signature;

        if (signature.IsDefaultOrEmpty)
        {
            throw new ArgumentNullException(
                nameof(signature),
                "Signature cannot be null or empty.");
        }
        else if (!Verify())
        {
            throw new ArgumentException("Signature is invalid.", nameof(signature));
        }
    }

    public int Height => _proposalMetadata.Height;

    public int Round => _proposalMetadata.Round;

    public BlockHash BlockHash => _proposalMetadata.BlockHash;

    public DateTimeOffset Timestamp => _proposalMetadata.Timestamp;

    public PublicKey ValidatorPublicKey => _proposalMetadata.ValidatorPublicKey;

    public byte[] MarshaledBlock => _proposalMetadata.MarshaledBlock;

    public int ValidRound => _proposalMetadata.ValidRound;

    public ImmutableArray<byte> Signature { get; }

    public bool Verify() =>
        !Signature.IsDefaultOrEmpty &&
        ValidatorPublicKey.Verify(
            ModelSerializer.SerializeToBytes(_proposalMetadata).ToImmutableArray(),
            Signature);

    public bool Equals(Proposal? other)
    {
        return other is Proposal proposal &&
               _proposalMetadata.Equals(proposal._proposalMetadata) &&
               Signature.SequenceEqual(proposal.Signature);
    }

    public override bool Equals(object? obj)
    {
        return obj is Proposal other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            _proposalMetadata.GetHashCode(),
            ByteUtility.CalculateHashCode(Signature.ToArray()));
    }
}
