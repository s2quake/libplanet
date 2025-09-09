using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Net;

[Model(Version = 1, TypeName = "Protocol")]
public sealed partial record class Protocol
{
    public static Protocol Empty { get; } = new Protocol
    {
        Metadata = new ProtocolMetadata(),
        Signature = [],
    };

    [Property(0)]
    public required ProtocolMetadata Metadata { get; init; }

    [Property(1)]
    public required ImmutableArray<byte> Signature { get; init; }

    public ProtocolHash Hash => new(SHA256.HashData(ModelSerializer.SerializeToBytes(this)));

    public Address Signer => Metadata.Signer;

    public int Version => Metadata.Version;

    public static Protocol Create(ISigner signer, int version)
    {
        var metadata = new ProtocolMetadata
        {
            Version = version,
            Signer = signer.Address,
        };
        return metadata.Sign(signer);
    }

    public bool Verify()
    {
        var bytes = ModelSerializer.SerializeToBytes(Metadata);
        return Signer.Verify(bytes, Signature.AsSpan());
    }

    public override string ToString() => $"{Signer},{Version},{ByteUtility.Hex(Signature)}";
}
