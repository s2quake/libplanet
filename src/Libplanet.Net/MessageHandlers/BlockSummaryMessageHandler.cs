using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockSummaryMessageHandler(
    Blockchain blockchain, ITransport transport, BlockDemandCollection blockDemands)
    : MessageHandlerBase<BlockSummaryMessage>
{
    private readonly BlockHash _genesisHash = blockchain.Genesis.BlockHash;

    public TimeSpan BlockDemandLifespan { get; set; } = TimeSpan.FromMinutes(1);

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

        var blockSummary = message.BlockSummary;
        var needed = IsBlockNeeded(blockSummary);

        blockSummary.Timestamp.ValidateTimestamp();
        if (needed)
        {
            var blockDemand = new BlockDemand(messageEnvelope.Sender, blockSummary, DateTimeOffset.UtcNow);
            blockDemands.Add(IsBlockNeeded, blockDemand);
        }
    }

    private bool IsBlockNeeded(BlockSummary blockSummary) => blockSummary.Height > blockchain.Tip.Height;

    private bool IsDemandNeeded(BlockDemand demand)
    {
        BlockDemand? oldDemand = blockDemands.Contains(demand.Peer)
            ? blockDemands[demand.Peer]
            : (BlockDemand?)null;

        bool needed;
        if (demand.IsStale(BlockDemandLifespan))
        {
            needed = false;
        }
        else if (IsBlockNeeded(demand.BlockSummary))
        {
            if (oldDemand is { } old)
            {
                needed = old.IsStale(BlockDemandLifespan) || old.Height < demand.Height;
            }
            else
            {
                needed = true;
            }
        }
        else
        {
            needed = false;
        }

        return needed;
    }
}
