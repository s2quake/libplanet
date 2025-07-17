using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net.Consensus.GossipMessageHandlers;

internal sealed class MessageHandler(Gossip gossip) : MessageHandlerBase<IMessage>
{
    protected override async ValueTask OnHandleAsync(
        IMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        await replyContext.PongAsync();
        gossip.AddMessage(message);
    }
}
