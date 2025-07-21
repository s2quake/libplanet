using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;

namespace Libplanet.Net.MessageHandlers;

internal sealed class TransactionRequestMessageHandler(Swarm swarm, SwarmOptions options)
    : MessageHandlerBase<TransactionRequestMessage>, IDisposable
{
    private readonly Blockchain _blockchain = swarm.Blockchain;
    private readonly ITransport _transport = swarm.Transport;
    private readonly AccessLimiter _accessLimiter = new(options.TaskRegulationOptions.MaxTransferBlocksTaskCount);

    public void Dispose()
    {
        _accessLimiter.Dispose();
    }

    protected override void OnHandle(
        TransactionRequestMessage message, MessageEnvelope messageEnvelope)
    {
        _ = OnHandleAsync(message, messageEnvelope, default).AsTask();
    }

    private async ValueTask OnHandleAsync(
        TransactionRequestMessage message, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        using var scope = await _accessLimiter.CanAccessAsync(cancellationToken);
        if (scope is null)
        {
            return;
        }

        var txIds = message.TxIds;
        var txs = txIds
            .Select(txId => _blockchain.Transactions.TryGetValue(txId, out var tx) ? tx : null)
            .OfType<Transaction>()
            .ToImmutableArray();

        var response = new TransactionResponseMessage
        {
            Transactions = txs,
        };
        _transport.Post(messageEnvelope.Sender, response, messageEnvelope.Identity);
    }
}
