using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Components.MessageHandlers;

internal sealed class PeerRequestMessageHandler(ITransport transport, PeerCollection peers)
    : MessageHandlerBase<PeerRequestMessage>
{
    protected override ValueTask OnHandleAsync(
        PeerRequestMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        if (messageEnvelope.Sender.Address == transport.Peer.Address)
        {
            throw new InvalidOperationException("Cannot receive ping from self.");
        }

        var target = message.Target;
        var k = message.K;
        var neighbors = peers.GetNeighbors(target, k, includeTarget: true);
        var peerMessage = new PeerResponseMessage { Peers = [.. neighbors] };
        transport.Post(messageEnvelope.Sender, peerMessage, messageEnvelope.Identity);

        return ValueTask.CompletedTask;
    }
}
