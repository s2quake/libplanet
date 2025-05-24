using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Libplanet;
using Libplanet.Net.Messages;
using Libplanet.Net.Transports;
using Libplanet.Serialization;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Transactions;
#if NETSTANDARD2_0
using Libplanet.Types;
#endif

namespace Libplanet.Net
{
    public partial class Swarm
    {
        public void BroadcastEvidence(IEnumerable<EvidenceBase> evidence)
        {
            BroadcastEvidence(null, evidence);
        }

        internal async IAsyncEnumerable<EvidenceBase> GetEvidenceAsync(
            BoundPeer peer,
            IEnumerable<EvidenceId> evidenceIds,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var evidenceIdsAsArray = evidenceIds as EvidenceId[] ?? evidenceIds.ToArray();
            var request = new GetEvidenceMessage { EvidenceIds = [.. evidenceIdsAsArray] };
            int evidenceCount = evidenceIdsAsArray.Count();

            _logger.Debug("Required evidence count: {Count}", evidenceCount);

            var evidenceRecvTimeout = Options.TimeoutOptions.GetTxsBaseTimeout
                + Options.TimeoutOptions.GetTxsPerTxIdTimeout.Multiply(evidenceCount);
            if (evidenceRecvTimeout > Options.TimeoutOptions.MaxTimeout)
            {
                evidenceRecvTimeout = Options.TimeoutOptions.MaxTimeout;
            }

            IEnumerable<Message> replies;
            try
            {
                replies = await Transport.SendMessageAsync(
                    peer,
                    request,
                    evidenceRecvTimeout,
                    evidenceCount,
                    true,
                    cancellationToken)
                .ConfigureAwait(false);
            }
            catch (CommunicationFailException e) when (e.InnerException is TimeoutException)
            {
                yield break;
            }

            foreach (Message message in replies)
            {
                if (message.Content is EvidenceMessage parsed)
                {
                    EvidenceBase evidence = ModelSerializer.DeserializeFromBytes<EvidenceBase>([.. parsed.Payload]);
                    yield return evidence;
                }
                else
                {
                    string errorMessage =
                        $"Expected {nameof(Transaction)} messages as response of " +
                        $"the {nameof(GetEvidenceMessage)} message, but got a " +
                        $"{message.GetType().Name} " +
                        $"message instead: {message}";
                    throw new InvalidMessageContentException(errorMessage, message.Content);
                }
            }
        }

        private void BroadcastEvidence(BoundPeer? except, IEnumerable<EvidenceBase> evidence)
        {
            List<EvidenceId> evidenceIds = evidence.Select(evidence => evidence.Id).ToList();
            _logger.Information("Broadcasting {Count} evidenceIds...", evidenceIds.Count);
            BroadcastEvidenceIds(except?.Address, evidenceIds);
        }

        private async Task BroadcastEvidenceAsync(
            TimeSpan broadcastTxInterval,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(broadcastTxInterval, cancellationToken);

                    await Task.Run(
                        () =>
                        {
                            List<EvidenceId> evidenceIds = BlockChain.PendingEvidences.Keys.ToList();

                            if (evidenceIds.Any())
                            {
                                _logger.Debug(
                                    "Broadcasting {EvidenceCount} pending evidence...",
                                    evidenceIds.Count);
                                BroadcastEvidenceIds(null, evidenceIds);
                            }
                        },
                        cancellationToken);
                }
                catch (OperationCanceledException e)
                {
                    _logger.Warning(e, "{MethodName}() was canceled", nameof(BroadcastTxAsync));
                    throw;
                }
                catch (Exception e)
                {
                    _logger.Error(
                        e,
                        "An unexpected exception occurred during {MethodName}()",
                        nameof(BroadcastTxAsync));
                }
            }
        }

        private void BroadcastEvidenceIds(Address? except, IEnumerable<EvidenceId> evidenceIds)
        {
            var message = new EvidenceIdsMessage { Ids = [.. evidenceIds] };
            BroadcastMessage(except, message);
        }

        private async Task TransferEvidenceAsync(Message message)
        {
            if (!await _transferEvidenceSemaphore.WaitAsync(TimeSpan.Zero, _cancellationToken))
            {
                _logger.Debug(
                    "Message {Message} is dropped due to task limit {Limit}",
                    message,
                    Options.TaskRegulationOptions.MaxTransferTxsTaskCount);
                return;
            }

            try
            {
                var getEvidenceMsg = (GetEvidenceMessage)message.Content;
                foreach (EvidenceId txid in getEvidenceMsg.EvidenceIds)
                {
                    try
                    {
                        EvidenceBase? ev = BlockChain.PendingEvidences[txid];

                        if (ev is null)
                        {
                            continue;
                        }

                        MessageContent response = new EvidenceMessage
                        {
                            Payload = [.. ModelSerializer.SerializeToBytes(ev)],
                        };
                        await Transport.ReplyMessageAsync(response, message.Identity, default);
                    }
                    catch (KeyNotFoundException)
                    {
                        _logger.Warning("Requested TxId {TxId} does not exist", txid);
                    }
                }
            }
            finally
            {
                int count = _transferEvidenceSemaphore.Release();
                if (count >= 0)
                {
                    _logger.Debug(
                        "{Count}/{Limit} tasks are remaining for handling {FName}",
                        count,
                        Options.TaskRegulationOptions.MaxTransferTxsTaskCount,
                        nameof(TransferEvidenceAsync));
                }
            }
        }

        private void ProcessEvidenceIds(Message message)
        {
            var evidenceIdsMsg = (EvidenceIdsMessage)message.Content;
            _logger.Information(
                "Received a {MessageType} message with {EvidenceIdCount} evidenceIds",
                nameof(EvidenceIdsMessage),
                evidenceIdsMsg.Ids.Count());

            EvidenceCompletion.Demand(message.Remote, evidenceIdsMsg.Ids);
        }
    }
}
