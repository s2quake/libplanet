using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net;

[Model(Version = 1, TypeName = "ProtocolVersion")]
public readonly partial record struct ProtocolVersion
{
    [Property(0)]
    public required ProtocolVersionMetadata Metadata { get; init; }

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

    public static ProtocolVersion FromToken(string token)
    {
        return ModelSerializer.DeserializeFromBytes<ProtocolVersion>(ByteUtility.ParseHex(token));
    }

    public static ProtocolVersion Create(PrivateKey signer, int version)
    {
        var metadata = new ProtocolVersionMetadata
        {
            Version = version,
            Signer = signer.Address,
        };
        return metadata.Sign(signer);
    }

    public static ProtocolVersion Create(PrivateKey signer, int version, object extra)
    {
        var metadata = new ProtocolVersionMetadata
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
