using Libplanet.Net;
using Libplanet.Types;

namespace Libplanet.Node.Tests;

internal sealed class RandomPrivateKey
{
    private readonly PrivateKey _privateKey = new();

    public PublicKey PublicKey => _privateKey.PublicKey;

    public Address Address => _privateKey.PublicKey.Address;

    public static implicit operator PrivateKey(RandomPrivateKey randomPrivateKey)
        => randomPrivateKey._privateKey;

    public override string ToString() => ByteUtility.Hex(_privateKey.Bytes);

    public ProtocolVersion ToAppProtocolVersion(int version)
    {
        return ProtocolVersion.Create(_privateKey, version);
    }
}
