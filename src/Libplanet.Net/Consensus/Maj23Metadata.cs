using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

[Model(Version = 1, TypeName = "Maj23Metadata")]
public sealed partial record class Maj23Metadata
{
    [Property(0)]
    [NonNegative]
    public int Height { get; init; }

    [Property(1)]
    [NonNegative]
    public int Round { get; init; }

    [Property(2)]
    [NotDefault]
    public required BlockHash BlockHash { get; init; }

    [Property(3)]
    [NotDefault]
    public required DateTimeOffset Timestamp { get; init; }

    [Property(4)]
    [NotDefault]
    public required Address Validator { get; init; }

    [Property(5)]
    [AllowedValues(VoteType.PreVote, VoteType.PreCommit)]
    public VoteType VoteType { get; init; }

    public Maj23 Sign(ISigner signer)
    {
        var options = new ModelOptions
        {
            IsValidationEnabled = true,
        };
        var message = ModelSerializer.SerializeToBytes(this, options);
        var signature = signer.Sign(message).ToImmutableArray();
        return new Maj23 { Metadata = this, Signature = signature };
    }
}
