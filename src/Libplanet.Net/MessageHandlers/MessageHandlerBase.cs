using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.MessageHandlers;

internal abstract class MessageHandlerBase<T> : IMessageHandler
    where T : IMessage
{
    Type IMessageHandler.MessageType { get; } = typeof(T);

    protected abstract ValueTask OnHandleAsync(
        T message, IReplyContext replyContext, CancellationToken cancellationToken);

    ValueTask IMessageHandler.HandleAsync(IReplyContext replyContext, CancellationToken cancellationToken)
        => OnHandleAsync((T)replyContext.Message, replyContext, cancellationToken);
}
