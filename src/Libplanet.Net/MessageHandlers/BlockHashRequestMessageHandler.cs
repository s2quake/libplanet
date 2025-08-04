using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockHashRequestMessageHandler(Blockchain blockchain, ITransport transport, int maxHashes = 100)
    : MessageHandlerBase<BlockHashRequestMessage>
{
    private readonly int _maxHashes = ValidateMaxHashes(maxHashes);

    protected override ValueTask OnHandleAsync(
        BlockHashRequestMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        var height = blockchain.Blocks[message.BlockHash].Height;
        var hashes = blockchain.Blocks[height..].Take(_maxHashes).Select(item => item.BlockHash).ToArray();
        var response = new BlockHashResponseMessage { BlockHashes = [.. hashes] };
        transport.Post(messageEnvelope.Sender, response, messageEnvelope.Identity);
        return ValueTask.CompletedTask;
    }

    private static int ValidateMaxHashes(int maxHashes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxHashes, 0);
        return maxHashes;
    }
}
