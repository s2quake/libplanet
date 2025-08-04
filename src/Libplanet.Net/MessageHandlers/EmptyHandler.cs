using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class EmptyHandler<T> : MessageHandlerBase<T>
    where T : IMessage
{
    protected override ValueTask OnHandleAsync(
        T message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}
