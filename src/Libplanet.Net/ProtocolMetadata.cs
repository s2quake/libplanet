using Libplanet.Serialization;
using Libplanet.Types.Crypto;

namespace Libplanet.Net;

[Model(Version = 1)]
public sealed record class ProtocolMetadata
{
    [Property(0)]
    public int Version { get; init; }

    [Property(1)]
    public required Address Signer { get; init; }

    [Property(3)]
    public ImmutableArray<byte> Extra { get; init; } = [];

    public Protocol Sign(PrivateKey signer)
    {
        var options = new ModelOptions
        {
            IsValidationEnabled = false,
        };
        var bytes = ModelSerializer.SerializeToBytes(this, options);
        var signature = signer.Sign(bytes).ToImmutableArray();
        return new Protocol { Metadata = this, Signature = signature };
    }
}
