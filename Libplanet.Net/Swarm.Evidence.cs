#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Net.Messages;
using Libplanet.Net.Transports;
using Libplanet.Types.Consensus;
using Libplanet.Types.Tx;
#if NETSTANDARD2_0
using Libplanet.Common;
#endif

namespace Libplanet.Net
{
    public partial class Swarm
    {
        internal async IAsyncEnumerable<Evidence> GetEvidencesAsync(
            BoundPeer peer,
            IEnumerable<EvidenceId> txIds,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var txIdsAsArray = txIds as EvidenceId[] ?? txIds.ToArray();
            var request = new GetEvidencesMsg(txIdsAsArray);
            int txCount = txIdsAsArray.Count();

            _logger.Debug("Required evidence count: {Count}", txCount);

            var txRecvTimeout = Options.TimeoutOptions.GetTxsBaseTimeout
                + Options.TimeoutOptions.GetTxsPerTxIdTimeout.Multiply(txCount);
            if (txRecvTimeout > Options.TimeoutOptions.MaxTimeout)
            {
                txRecvTimeout = Options.TimeoutOptions.MaxTimeout;
            }

            IEnumerable<Message> replies;
            try
            {
                replies = await Transport.SendMessageAsync(
                    peer,
                    request,
                    txRecvTimeout,
                    txCount,
                    true,
                    cancellationToken
                ).ConfigureAwait(false);
            }
            catch (CommunicationFailException e) when (e.InnerException is TimeoutException)
            {
                yield break;
            }

            foreach (Message message in replies)
            {
                if (message.Content is EvidenceMsg parsed)
                {
                    Evidence evidence = Evidence.Deserialize(parsed.Payload);
                    yield return evidence;
                }
                else
                {
                    string errorMessage =
                        $"Expected {nameof(Transaction)} messages as response of " +
                        $"the {nameof(GetTxsMsg)} message, but got a {message.GetType().Name} " +
                        $"message instead: {message}";
                    throw new InvalidMessageContentException(errorMessage, message.Content);
                }
            }
        }

        private void BroadcastEvidences(BoundPeer except, IEnumerable<Evidence> evidences)
        {
            List<EvidenceId> evidenceIds = evidences.Select(evidence => evidence.Id).ToList();
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
                            List<EvidenceId> evidenceIds = BlockChain
                                .GetPendingEvidences()
                                .Select(item => item.Id)
                                .ToList();

                            if (evidenceIds.Any())
                            {
                                _logger.Debug(
                                    "Broadcasting {EvidenceCount} pending evidences...",
                                    evidenceIds.Count);
                                BroadcastEvidenceIds(null, evidenceIds);
                            }
                        }, cancellationToken);
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
            var message = new EvidenceIdsMsg(evidenceIds);
            BroadcastMessage(except, message);
        }
    }
}
