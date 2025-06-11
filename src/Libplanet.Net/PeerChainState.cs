namespace Libplanet.Net;

public readonly record struct PeerChainState(BoundPeer Peer, long TipIndex)
{
    public override string ToString() => $"{Peer}, {TipIndex}";
}
