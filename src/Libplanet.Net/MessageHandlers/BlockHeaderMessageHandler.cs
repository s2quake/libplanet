using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockHeaderMessageHandler(
    Blockchain blockchain, ITransport transport, BlockDemandCollection blockDemands)
    : MessageHandlerBase<BlockSummaryMessage>
{
    private readonly BlockHash _genesisHash = blockchain.Genesis.BlockHash;

    internal BlockHeaderMessageHandler(Swarm swarm)
        : this(swarm.Blockchain, swarm.Transport, swarm.BlockDemandDictionary)
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
            var blockDemand = new BlockDemand(blockSummary, messageEnvelope.Sender, DateTimeOffset.UtcNow);
            blockDemands.Add(IsBlockNeeded, blockDemand);
        }
    }

    private bool IsBlockNeeded(BlockSummary blockSummary) => blockSummary.Height > blockchain.Tip.Height;
}
