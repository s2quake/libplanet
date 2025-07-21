using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockHashMessageHandler
    : MessageHandlerBase<BlockHashResponseMessage>
{
    protected override void OnHandle(BlockHashResponseMessage message, MessageEnvelope messageEnvelope)
    {
    }
}
