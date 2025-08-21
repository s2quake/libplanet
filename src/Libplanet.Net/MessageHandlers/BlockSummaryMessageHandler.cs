using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockSummaryMessageHandler(
    Blockchain blockchain, BlockDemandCollection blockDemands)
    : MessageHandlerBase<BlockSummaryMessage>
{
    private readonly BlockHash _genesisHash = blockchain.Genesis.BlockHash;

    public TimeSpan BlockDemandLifespan { get; init; } = TimeSpan.FromMinutes(1);

    protected override ValueTask OnHandleAsync(
        BlockSummaryMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        if (message.GenesisBlockHash != _genesisHash)
        {
            throw new InvalidMessageException("Invalid block header message.");
        }

        var blockSummary = ValidateAndReturn(message.BlockSummary);
        var blockDemand = new BlockDemand(messageEnvelope.Sender, blockSummary, DateTimeOffset.UtcNow);
        blockDemands.AddOrUpdate(blockDemand);

        return ValueTask.CompletedTask;
    }

    private static BlockSummary ValidateAndReturn(BlockSummary blockSummary)
    {
        blockSummary.Timestamp.ValidateTimestamp();
        return blockSummary;
    }
}
