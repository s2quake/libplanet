using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

[Model(Version = 1, TypeName = "ProposalMetadata")]
public sealed partial record class ProposalMetadata : IValidatableObject
{
    [Property(0)]
    public required BlockHash BlockHash { get; init; }

    [Property(1)]
    public HashDigest<SHA256> StateRootHash { get; init; }

    [Property(2)]
    [NonNegative]
    public int Height { get; init; }

    [Property(3)]
    [NonNegative]
    public int Round { get; init; }

    [Property(4)]
    public DateTimeOffset Timestamp { get; init; }

    [Property(5)]
    public Address Proposer { get; init; }

    [Property(6)]
    public int ValidRound { get; init; }

    public Proposal Sign(ISigner signer, Block block)
    {
        if (Height != block.Height)
        {
            throw new ArgumentException($"Height mismatch: {Height} != {block.Height}", nameof(block));
        }

        if (block.BlockHash != BlockHash)
        {
            throw new ArgumentException($"BlockHash mismatch: {BlockHash} != {block.BlockHash}", nameof(block));
        }

        if (block.Proposer != Proposer)
        {
            throw new ArgumentException($"Proposer mismatch: {Proposer} != {block.Proposer}", nameof(block));
        }

        var options = new ModelOptions
        {
            IsValidationEnabled = true,
        };
        var bytes = ModelSerializer.SerializeToBytes(this, options);
        var signature = signer.Sign(bytes).ToImmutableArray();
        var marshaledBlock = ModelSerializer.SerializeToBytes(block, options).ToImmutableArray();
        return new Proposal { Metadata = this, Signature = signature, MarshaledBlock = marshaledBlock };
    }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (ValidRound < -1)
        {
            yield return new ValidationResult("ValidRound must be greater than or equal to -1.", [nameof(ValidRound)]);
        }
    }
}
