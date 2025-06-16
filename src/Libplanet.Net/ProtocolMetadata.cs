using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;

namespace Libplanet.Net;

[Model(Version = 1, TypeName = "ProtocolMetadata")]
public sealed partial record class ProtocolMetadata
{
    [Property(0)]
    [NonNegative]
    public int Version { get; init; }

    [Property(1)]
    public Address Signer { get; init; }

    [Property(2)]
    [NotDefault]
    public ImmutableArray<byte> Extra { get; init; } = [];

    public Protocol Sign(PrivateKey signer)
    {
        var options = new ModelOptions
        {
            IsValidationEnabled = true,
        };
        var bytes = ModelSerializer.SerializeToBytes(this, options);
        var signature = signer.Sign(bytes).ToImmutableArray();
        return new Protocol { Metadata = this, Signature = signature };
    }
}
