using System.Reactive;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class BlockHeaderMessageHandler(Blockchain blockchain, BlockDemandDictionary blockDemandDictionary)
    : MessageHandlerBase<BlockHeaderMessage>
{
    private readonly Subject<Unit> _blockHeaderReceivedSubject = new();

    protected override async ValueTask OnHandleAsync(
        BlockHeaderMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        try
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
                    IsBlockNeeded, new BlockDemand(header, replyContext.Sender, DateTimeOffset.UtcNow));
            }
        }
        finally
        {
            await replyContext.PongAsync();
        }
    }

    private bool IsBlockNeeded(BlockSummary blockSummary) => blockSummary.Height > blockchain.Tip.Height;
}
