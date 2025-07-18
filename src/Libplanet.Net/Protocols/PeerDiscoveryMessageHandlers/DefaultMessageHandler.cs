using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Net.Protocols;
using Libplanet.Types;

namespace Libplanet.Net.Protocols.PeerDiscoveryMessageHandlers;

internal sealed class DefaultMessageHandler(Address owner, RoutingTable table, RoutingTable replacementCache)
    : MessageHandlerBase<IMessage>
{
    protected override void OnHandle(IMessage message, MessageEnvelope messageEnvelope)
    {
        if (messageEnvelope.Sender.Address != owner)
        {
            var peer = messageEnvelope.Sender;
            var peerState = table.TryGetValue(peer.Address, out var v)
                ? v with { LastUpdated = DateTimeOffset.UtcNow }
                : new PeerState { Peer = peer, LastUpdated = DateTimeOffset.UtcNow };

            if (!table.AddOrUpdate(peerState) && !replacementCache.AddOrUpdate(peerState))
            {
                var bucket = replacementCache.Buckets[peer.Address];
                var oldestPeerState = bucket.Oldest;
                var oldestAddress = oldestPeerState.Address;
                bucket.Remove(oldestAddress);
                bucket.AddOrUpdate(peerState);
            }
        }
    }
}
