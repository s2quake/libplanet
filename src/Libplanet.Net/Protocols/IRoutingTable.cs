namespace Libplanet.Net.Protocols;

public interface IRoutingTable
{
    int Count { get; }

    IReadOnlyList<Peer> Peers { get; }

    void AddPeer(Peer peer);

    bool RemovePeer(Peer peer);

    bool Contains(Peer peer);
}
