using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Components.MessageHandlers;

internal sealed class DefaultMessageHandler(PeerExplorer peerExplorer)
    : MessageHandlerBase<IMessage>
{
    protected override ValueTask OnHandleAsync(
        IMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        if (messageEnvelope.Sender != peerExplorer.Peer)
        {
            peerExplorer.AddOrUpdate(messageEnvelope.Sender);
        }

        return ValueTask.CompletedTask;
    }
}
