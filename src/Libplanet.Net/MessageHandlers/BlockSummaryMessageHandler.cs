using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockSummaryMessageHandler(
    Blockchain blockchain, BlockDemandCollection blockDemands)
    : MessageHandlerBase<BlockSummaryMessage>
{
    private readonly BlockHash _genesisHash = blockchain.Genesis.BlockHash;

    public TimeSpan BlockDemandLifespan { get; init; } = TimeSpan.FromMinutes(1);

    internal BlockSummaryMessageHandler(Swarm swarm)
        : this(swarm.Blockchain, swarm.BlockDemands)
    {
    }

    protected override void OnHandle(BlockSummaryMessage message, MessageEnvelope messageEnvelope)
    {
        if (message.GenesisHash != _genesisHash)
        {
            throw new InvalidMessageException("Invalid block header message.");
        }

        var blockSummary = ValidateAndReturn(message.BlockSummary);
        var blockDemand = new BlockDemand(messageEnvelope.Sender, blockSummary, DateTimeOffset.UtcNow);
        blockDemands.AddOrUpdate(blockDemand);
    }

    private static BlockSummary ValidateAndReturn(BlockSummary blockSummary)
    {
        blockSummary.Timestamp.ValidateTimestamp();
        return blockSummary;
    }
}
