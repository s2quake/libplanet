using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net;

[Model(Version = 1, TypeName = "Protocol")]
public readonly partial record struct Protocol
{
    [Property(0)]
    public required ProtocolMetadata Metadata { get; init; }

    [Property(1)]
    public required ImmutableArray<byte> Signature { get; init; }

    public Address Signer => Metadata.Signer;

    public int Version => Metadata.Version;

    public string Token
    {
        get
        {
            return ByteUtility.Hex(ModelSerializer.SerializeToBytes(this));
        }
    }

    public static Protocol FromToken(string token)
    {
        return ModelSerializer.DeserializeFromBytes<Protocol>(ByteUtility.ParseHex(token));
    }

    public static Protocol Create(PrivateKey signer, int version)
    {
        var metadata = new ProtocolMetadata
        {
            Version = version,
            Signer = signer.Address,
        };
        return metadata.Sign(signer);
    }

    public static Protocol Create(PrivateKey signer, int version, object extra)
    {
        var metadata = new ProtocolMetadata
        {
            Version = version,
            Signer = signer.Address,
            Extra = [.. ModelSerializer.SerializeToBytes(extra)],
        };
        return metadata.Sign(signer);
    }

    public bool Verify()
    {
        var bytes = ModelSerializer.SerializeToBytes(Metadata);
        return Signer.Verify(bytes, Signature.AsSpan());
    }
}
