using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;

namespace Libplanet.Net.Consensus;

[Model(Version = 1)]
public sealed record class Maj23Metadata
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
    [DisallowedEnumValues(VoteFlag.Null, VoteFlag.Unknown)]
    public VoteFlag Flag { get; init; }

    public Maj23 Sign(PrivateKey signer)
    {
        var options = new ModelOptions
        {
            IsValidationEnabled = true,
        };
        var bytes = ModelSerializer.SerializeToBytes(this, options);
        var signature = signer.Sign(bytes).ToImmutableArray();
        return new Maj23 { Metadata = this, Signature = signature };
    }
}
