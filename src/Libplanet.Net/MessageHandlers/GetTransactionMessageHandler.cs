using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class GetTransactionMessageHandler(Blockchain blockchain, AccessLimiter accessLimiter)
    : MessageHandlerBase<GetTransactionMessage>
{
    protected override async ValueTask OnHandleAsync(
        GetTransactionMessage message, IReplyContext replyContext, CancellationToken cancellationToken)
    {
        using var scope = await accessLimiter.CanAccessAsync(cancellationToken);
        if (scope is null)
        {
            return;
        }

        var txIds = message.TxIds;
        var txs = txIds
            .Select(txId => blockchain.Transactions.TryGetValue(txId, out var tx) ? tx : null)
            .OfType<Transaction>()
            .ToArray();
        await replyContext.TransferAsync(txs);
    }
}
