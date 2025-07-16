using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class PingMessageHandler
    : MessageHandlerBase<PingMessage>
{
    protected override async ValueTask OnHandleAsync(
        PingMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        await replyContext.PongAsync();
    }
}
