using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;

namespace Libplanet.Types;

[Model(Version = 1, TypeName = "VoteMetadata")]
public sealed partial record class VoteMetadata
{
    [Property(0)]
    public required Address Validator { get; init; }

    [Property(1)]
    [NotDefault]
    public BlockHash BlockHash { get; init; }

    [Property(2)]
    [NonNegative]
    public int Height { get; init; }

    [Property(3)]
    [NonNegative]
    public int Round { get; init; }

    [Property(4)]
    public DateTimeOffset Timestamp { get; init; }

    [Property(5)]
    [Positive]
    public BigInteger ValidatorPower { get; init; }

    [Property(6)]
    public VoteType Type { get; init; }

    public bool Verify(ReadOnlySpan<byte> signature)
    {
        var message = ModelSerializer.SerializeToBytes(this);
        return Validator.Verify(message, signature);
    }

    public Vote Sign(ISigner signer)
    {
        var options = new ModelOptions
        {
            IsValidationEnabled = true,
        };
        var message = ModelSerializer.SerializeToBytes(this, options);
        var signature = signer.Sign(message);
        return new Vote { Metadata = this, Signature = [.. signature] };
    }

    public Vote WithoutSignature() => new() { Metadata = this, Signature = [] };
}
