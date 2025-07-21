using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockHashRequestMessageHandler(Swarm swarm)
    : MessageHandlerBase<BlockHashRequestMessage>
{
    private readonly Blockchain _blockchain = swarm.Blockchain;
    private readonly ITransport _transport = swarm.Transport;

    protected override void OnHandle(BlockHashRequestMessage message, MessageEnvelope messageEnvelope)
    {
        var height = _blockchain.Blocks[message.BlockHash].Height;
        var hashes = _blockchain.Blocks[height..].Select(item => item.BlockHash).ToArray();
        var response = new BlockHashResponseMessage { BlockHashes = [.. hashes] };
        _transport.Post(messageEnvelope.Sender, response, messageEnvelope.Identity);
    }
}
