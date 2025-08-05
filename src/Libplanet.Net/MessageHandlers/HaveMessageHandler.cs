using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class HaveMessageHandler(
    PeerCollection peers, MessageCollection messages, PeerMessageIdCollection peerMessageIds)
    : MessageHandlerBase<HaveMessage>
{
    protected override async ValueTask OnHandleAsync(
        HaveMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var ids = message.Ids.Where(id => !messages.Contains(id)).ToImmutableArray();
        var peer = messageEnvelope.Sender;

        if (ids.Length is not 0)
        {
            peerMessageIds.Add(peer, ids);
        }

        peers.Add(messageEnvelope.Sender);

        await ValueTask.CompletedTask;
    }
}
