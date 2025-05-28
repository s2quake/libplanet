using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Net.Consensus;

[Model(Version = 1)]
public sealed partial record class ProposalMetadata : IValidatableObject
{
    [Property(0)]
    public BlockHash BlockHash { get; init; }

    [Property(1)]
    public HashDigest<SHA256> StateRootHash { get; init; }

    [Property(0)]
    [NonNegative]
    public int Height { get; init; }

    [Property(1)]
    [NonNegative]
    public int Round { get; init; }

    [Property(3)]
    public DateTimeOffset Timestamp { get; init; }

    [Property(4)]
    public Address Proposer { get; init; }

    [Property(6)]
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
