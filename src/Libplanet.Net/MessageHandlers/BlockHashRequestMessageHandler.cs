using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockHashRequestMessageHandler(Blockchain blockchain, ITransport transport)
    : MessageHandlerBase<BlockHashRequestMessage>
{
    internal BlockHashRequestMessageHandler(Swarm swarm)
        : this(swarm.Blockchain, swarm.Transport)
    {
    }

    protected override void OnHandle(BlockHashRequestMessage message, MessageEnvelope messageEnvelope)
    {
        var height = blockchain.Blocks[message.BlockHash].Height;
        var hashes = blockchain.Blocks[height..].Select(item => item.BlockHash).ToArray();
        var response = new BlockHashResponseMessage { BlockHashes = [.. hashes] };
        transport.Post(messageEnvelope.Sender, response, messageEnvelope.Identity);
    }
}
