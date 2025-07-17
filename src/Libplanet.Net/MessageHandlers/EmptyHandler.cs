using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.MessageHandlers;

internal sealed class EmptyHandler<T> : MessageHandlerBase<T>
    where T : IMessage
{
    protected override async ValueTask OnHandleAsync(
        T message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        await ValueTask.CompletedTask;
    }
}
