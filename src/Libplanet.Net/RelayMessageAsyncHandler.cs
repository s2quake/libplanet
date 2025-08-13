using Libplanet.Net.Messages;

namespace Libplanet.Net;

public sealed class RelayMessageAsyncHandler<T>(Func<T, MessageEnvelope, CancellationToken, Task> func)
    : IMessageHandler
    where T : IMessage
{
    public RelayMessageAsyncHandler(Func<T, CancellationToken, Task> func)
        : this((m, _, c) => func(m, c))
    {
    }

    Type IMessageHandler.MessageType => typeof(T);

    async ValueTask IMessageHandler.HandleAsync(MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        await func((T)messageEnvelope.Message, messageEnvelope, cancellationToken);
        await ValueTask.CompletedTask;
    }
}
