using System.Reactive;
using System.Reactive.Subjects;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockHeaderMessageHandler(Blockchain blockchain, BlockDemandCollection blockDemandDictionary)
    : MessageHandlerBase<BlockHeaderMessage>
{
    private readonly Subject<Unit> _blockHeaderReceivedSubject = new();

    protected override void OnHandle(BlockHeaderMessage message, MessageEnvelope messageEnvelope)
    {
        if (!message.GenesisHash.Equals(blockchain.Genesis.BlockHash))
        {
            return;
        }

        _blockHeaderReceivedSubject.OnNext(Unit.Default);
        var header = message.BlockSummary;

        try
        {
            header.Timestamp.ValidateTimestamp();
        }
        catch (InvalidOperationException e)
        {
            return;
        }

        bool needed = IsBlockNeeded(header);
        if (needed)
        {
            blockDemandDictionary.Add(
                IsBlockNeeded, new BlockDemand(header, messageEnvelope.Sender, DateTimeOffset.UtcNow));
        }
    }

    private bool IsBlockNeeded(BlockSummary blockSummary) => blockSummary.Height > blockchain.Tip.Height;
}
