using System.Net;
using Libplanet.Net;
using Libplanet.Types;

namespace Libplanet.Node.Tests;

internal sealed class RandomBoundPeer : IDisposable
{
    private readonly Peer _boundPeer;
    private readonly PrivateKey _privateKey = new();
    private readonly RandomEndPoint _endPoint = new();

    public RandomBoundPeer() => _boundPeer = new Peer { Address = PublicKey.Address, EndPoint = _endPoint };

    public RandomBoundPeer(PrivateKey privateKey)
    {
        _privateKey = privateKey;
        _boundPeer = new Peer { Address = PublicKey.Address, EndPoint = _endPoint };
    }

    public PrivateKey PrivateKey => _privateKey;

    public PublicKey PublicKey => _privateKey.PublicKey;

    public EndPoint EndPoint => _endPoint;

    public static implicit operator Peer(RandomBoundPeer randomBoundPeer)
        => randomBoundPeer._boundPeer;

    public override string ToString() => $"{PublicKey}, {_endPoint}";

    public void Dispose() => _endPoint.Dispose();
}
