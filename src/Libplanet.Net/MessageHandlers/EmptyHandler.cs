using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class EmptyHandler<T> : MessageHandlerBase<T>
    where T : IMessage
{
    protected override void OnHandle(T message, MessageEnvelope messageEnvelope)
    {
    }
}
