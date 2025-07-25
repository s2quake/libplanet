using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockSummaryMessageHandler(
    Blockchain blockchain, ITransport transport, BlockDemandCollection blockDemands)
    : MessageHandlerBase<BlockSummaryMessage>
{
    private readonly BlockHash _genesisHash = blockchain.Genesis.BlockHash;

    public TimeSpan BlockDemandLifespan { get; init; } = TimeSpan.FromMinutes(1);

    internal BlockSummaryMessageHandler(Swarm swarm)
        : this(swarm.Blockchain, swarm.Transport, swarm.BlockDemands)
    {
    }

    protected override void OnHandle(BlockSummaryMessage message, MessageEnvelope messageEnvelope)
    {
        using var pongScope = new PongScope(transport, messageEnvelope);
        if (message.GenesisHash != _genesisHash)
        {
            throw new InvalidMessageException("Invalid block header message.");
        }

        var blockSummary = ValidateAndReturn(message.BlockSummary);
        var blockDemand = new BlockDemand(messageEnvelope.Sender, blockSummary, DateTimeOffset.UtcNow);
        // if (IsDemandNeeded(blockDemand))
        // {
            blockDemands.AddOrUpdate(blockDemand);
        // }
    }

    // private bool IsDemandNeeded(BlockDemand blockDemand)
    // {
    //     if (blockDemand.IsStale(BlockDemandLifespan))
    //     {
    //         return false;
    //     }

    //     if (blockDemand.Height <= blockchain.Tip.Height)
    //     {
    //         return false;
    //     }

    //     if (blockDemands.TryGetValue(blockDemand.Peer, out var oldBlockDemand))
    //     {
    //         return oldBlockDemand.IsStale(BlockDemandLifespan) || oldBlockDemand.Height < blockDemand.Height;
    //     }

    //     return true;
    // }

    private static BlockSummary ValidateAndReturn(BlockSummary blockSummary)
    {
        blockSummary.Timestamp.ValidateTimestamp();
        return blockSummary;
    }
}
