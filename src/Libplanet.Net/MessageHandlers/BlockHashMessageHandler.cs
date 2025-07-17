using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockHashMessageHandler
    : MessageHandlerBase<BlockHashMessage>
{
    protected override void OnHandle(BlockHashMessage message, MessageEnvelope messageEnvelope)
    {
    }
}
