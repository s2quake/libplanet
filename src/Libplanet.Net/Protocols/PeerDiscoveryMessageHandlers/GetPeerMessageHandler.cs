using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Net.Protocols;
using Libplanet.Types;

namespace Libplanet.Net.Protocols.PeerDiscoveryMessageHandlers;

internal sealed class GetPeerMessageHandler(Address owner, RoutingTable table)
    : MessageHandlerBase<GetPeerMessage>
{
    protected override void OnHandle(
        GetPeerMessage message, MessageEnvelope messageEnvelope)
    {
        if (messageEnvelope.Sender.Address.Equals(owner))
        {
            throw new InvalidOperationException("Cannot receive ping from self.");
        }

        var target = message.Target;
        var k = RoutingTable.BucketCount;
        var peers = table.GetNeighbors(target, k, includeTarget: true);
        var peerMessage = new PeerMessage { Peers = [.. peers] };
        // await replyContext.CompleteAsync(peerMessage);
    }
}
