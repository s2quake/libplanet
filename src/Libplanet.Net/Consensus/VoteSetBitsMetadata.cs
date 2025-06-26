using System.ComponentModel.DataAnnotations;
using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

[Model(Version = 1, TypeName = "VoteSetBitsMetadata")]
public sealed partial record class VoteSetBitsMetadata
{
    [Property(0)]
    [NonNegative]
    public int Height { get; init; }

    [Property(1)]
    [NonNegative]
    public int Round { get; init; }

    [Property(2)]
    [NotDefault]
    public BlockHash BlockHash { get; init; }

    [Property(3)]
    public DateTimeOffset Timestamp { get; init; }

    [Property(4)]
    [NotDefault]
    public Address Validator { get; init; }

    [Property(5)]
    [AllowedValues(VoteType.PreVote, VoteType.PreCommit)]
    public VoteType VoteType { get; init; }

    public ImmutableArray<bool> VoteBits { get; init; }

    public VoteSetBits Sign(ISigner signer)
    {
        var options = new ModelOptions
        {
            IsValidationEnabled = true,
        };
        var bytes = ModelSerializer.SerializeToBytes(this, options);
        var signature = signer.Sign(bytes).ToImmutableArray();
        return new VoteSetBits { Metadata = this, Signature = signature };
    }
}
