using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockHashMessageHandler
    : MessageHandlerBase<BlockHashResponseMessage>
{
    protected override ValueTask OnHandleAsync(
        BlockHashResponseMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
