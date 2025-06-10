using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net;

[Model(Version = 1, TypeName = "ProtocolMetadata")]
public readonly partial record struct ProtocolMetadata
{
    public ProtocolMetadata()
    {
    }

    [Property(0)]
    public int Version { get; init; }

    [Property(1)]
    public required Address Signer { get; init; }

    [Property(2)]
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
