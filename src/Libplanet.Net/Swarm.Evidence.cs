using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Transports;
using Libplanet.Serialization;
using Libplanet.Types;
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
            Peer peer,
            IEnumerable<EvidenceId> evidenceIds,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var evidenceIdsAsArray = evidenceIds as EvidenceId[] ?? evidenceIds.ToArray();
            var request = new GetEvidenceMessage { EvidenceIds = [.. evidenceIdsAsArray] };
            int evidenceCount = evidenceIdsAsArray.Count();

            var evidenceRecvTimeout = Options.TimeoutOptions.GetTxsBaseTimeout
                + Options.TimeoutOptions.GetTxsPerTxIdTimeout.Multiply(evidenceCount);
            if (evidenceRecvTimeout > Options.TimeoutOptions.MaxTimeout)
            {
                evidenceRecvTimeout = Options.TimeoutOptions.MaxTimeout;
            }

            var messageEnvelope = await Transport.SendMessageAsync(peer, request, cancellationToken);
            var aggregateMessage = (AggregateMessage)messageEnvelope.Message;

            foreach (var message in aggregateMessage.Messages)
            {
                if (message is EvidenceMessage parsed)
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
                    throw new InvalidOperationException(errorMessage);
                }
            }
        }

        private void BroadcastEvidence(Peer? except, IEnumerable<EvidenceBase> evidence)
        {
            List<EvidenceId> evidenceIds = evidence.Select(evidence => evidence.Id).ToList();
            BroadcastEvidenceIds(except?.Address ?? default, evidenceIds);
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
                            List<EvidenceId> evidenceIds = Blockchain.PendingEvidences.Keys.ToList();

                            if (evidenceIds.Any())
                            {
                                BroadcastEvidenceIds(default, evidenceIds);
                            }
                        },
                        cancellationToken);
                }
                catch (OperationCanceledException e)
                {
                    throw;
                }
                catch (Exception e)
                {
                }
            }
        }

        private void BroadcastEvidenceIds(Address except, IEnumerable<EvidenceId> evidenceIds)
        {
            var message = new EvidenceIdsMessage { Ids = [.. evidenceIds] };
            BroadcastMessage(except, message);
        }

        private async Task TransferEvidenceAsync(MessageEnvelope message)
        {
            if (!await _transferEvidenceSemaphore.WaitAsync(TimeSpan.Zero, _cancellationToken))
            {
                return;
            }

            try
            {
                var getEvidenceMsg = (GetEvidenceMessage)message.Message;
                foreach (EvidenceId txid in getEvidenceMsg.EvidenceIds)
                {
                    try
                    {
                        EvidenceBase? ev = Blockchain.PendingEvidences[txid];

                        if (ev is null)
                        {
                            continue;
                        }

                        MessageBase response = new EvidenceMessage
                        {
                            Payload = [.. ModelSerializer.SerializeToBytes(ev)],
                        };
                        Transport.ReplyMessage(message.Identity, response);
                    }
                    catch (KeyNotFoundException)
                    {
                    }
                }
            }
            finally
            {
                int count = _transferEvidenceSemaphore.Release();
                if (count >= 0)
                {
                }
            }
        }

        private void ProcessEvidenceIds(MessageEnvelope message)
        {
            var evidenceIdsMsg = (EvidenceIdsMessage)message.Message;
            EvidenceCompletion.Demand(message.Peer, evidenceIdsMsg.Ids);
        }
    }
}
