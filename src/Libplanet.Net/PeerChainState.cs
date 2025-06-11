namespace Libplanet.Net;

public readonly record struct PeerChainState(Peer Peer, long TipIndex)
{
    public override string ToString() => $"{Peer}, {TipIndex}";
}
