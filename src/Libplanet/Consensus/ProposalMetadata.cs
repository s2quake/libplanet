using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Consensus;

public sealed record class ProposalMetadata : IValidatableObject
{
    [NonNegative]
    public int Height { get; init; }

    [NonNegative]
    public int Round { get; init; }

    public BlockHash BlockHash { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public Address Validator { get; init; }

    public byte[] MarshaledBlock { get; init; } = [];

    public int ValidRound { get; init; }

    public Proposal Sign(PrivateKey signer)
    {
        var bytes = ModelSerializer.SerializeToBytes(this);
        var signature = signer.Sign(bytes).ToImmutableArray();
        return new Proposal { Metadata = this, Signature = signature };
    }

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (ValidRound < -1)
        {
            yield return new ValidationResult("ValidRound must be greater than or equal to -1.", [nameof(ValidRound)]);
        }
    }
}
