using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockHashMessageHandler
    : MessageHandlerBase<BlockHashMessage>
{
    protected override async ValueTask OnHandleAsync(
        BlockHashMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        await ValueTask.CompletedTask;
    }
}
