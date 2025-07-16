using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net.MessageHandlers;

internal sealed class TxIdMessageHandler(TxFetcher txFetcher)
    : MessageHandlerBase<TxIdMessage>
{
    protected override async ValueTask OnHandleAsync(
        TxIdMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        txFetcher.DemandMany(replyContext.Sender, [.. message.Ids]);
        await replyContext.PongAsync();
    }
}
