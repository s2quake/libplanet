namespace Libplanet.Net.Protocols;

public interface IRoutingTable
{
    int Count { get; }

    IReadOnlyList<BoundPeer> Peers { get; }

    void AddPeer(BoundPeer peer);

    bool RemovePeer(BoundPeer peer);

    bool Contains(BoundPeer peer);
}
