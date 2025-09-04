using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class TransactionRequestMessageHandler(
    Blockchain blockchain, ITransport transport, int maxConcurrentResponses)
    : MessageHandlerBase<TransactionRequestMessage>, IDisposable
{
    private readonly AccessLimiter _accessLimiter = new(maxConcurrentResponses);

    public void Dispose() => _accessLimiter.Dispose();

    protected override async ValueTask OnHandleAsync(
        TransactionRequestMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        using var scope = await _accessLimiter.CanAccessAsync(cancellationToken);
        if (scope is null)
        {
            return;
        }

        var txIds = message.TxIds;
        var txList = new List<Transaction>();
        foreach (var txId in txIds)
        {
            if (blockchain.Transactions.TryGetValue(txId, out var transaction))
            {
                txList.Add(transaction);
            }
            else if (blockchain.StagedTransactions.TryGetValue(txId, out var stagedTransaction)
                && blockchain.GetTxNonce(stagedTransaction.Signer) <= stagedTransaction.Nonce
                && blockchain.StagedTransactions.IsValid(stagedTransaction))
            {
                txList.Add(stagedTransaction);
            }

            if (txList.Count == message.ChunkSize)
            {
                var response = new TransactionResponseMessage
                {
                    Transactions = [.. txList],
                };
                transport.Post(messageEnvelope.Sender, response, messageEnvelope.Identity);
                txList.Clear();
                await Task.Yield();
            }
        }

        var lastResponse = new TransactionResponseMessage
        {
            Transactions = [.. txList],
            IsLast = true,
        };
        transport.Post(messageEnvelope.Sender, lastResponse, messageEnvelope.Identity);
        await Task.Yield();
    }
}
