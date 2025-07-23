using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.PeerServiceMessageHandlers;

internal sealed class GetPeerMessageHandler(ITransport transport, PeerCollection table)
    : MessageHandlerBase<GetPeerMessage>
{
    protected override void OnHandle(GetPeerMessage message, MessageEnvelope messageEnvelope)
    {
        if (messageEnvelope.Sender.Address == transport.Peer.Address)
        {
            throw new InvalidOperationException("Cannot receive ping from self.");
        }

        var target = message.Target;
        var k = PeerCollection.BucketCount;
        var peers = table.GetNeighbors(target, k, includeTarget: true);
        var peerMessage = new PeerMessage { Peers = [.. peers] };
        transport.Post(messageEnvelope.Sender, peerMessage, messageEnvelope.Identity);
    }
}
