using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Libplanet.Blockchain;
using Libplanet.Types.Tx;
using Nito.AsyncEx;
using Serilog;

namespace Libplanet.Net
{
    public class TxCompletion<TPeer> : IDisposable
        where TPeer : notnull
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly BlockChain _blockChain;
        private readonly TxFetcher _txFetcher;
        private readonly TxBroadcaster _txBroadcaster;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<TPeer, TxFetchJob> _txFetchJobs;

        private bool _disposed;

        public TxCompletion(
            BlockChain blockChain,
            TxFetcher txFetcher,
            TxBroadcaster txBroadcaster)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _blockChain = blockChain;
            _txFetcher = txFetcher;
            _txBroadcaster = txBroadcaster;
            _txFetchJobs = new ConcurrentDictionary<TPeer, TxFetchJob>();
            TxReceived = new AsyncAutoResetEvent();

            _logger = Log
                .ForContext<TxCompletion<TPeer>>()
                .ForContext("Source", nameof(TxCompletion<TPeer>));
        }

        public delegate IAsyncEnumerable<Transaction> TxFetcher(
            TPeer peer,
            IEnumerable<TxId> txIds,
            CancellationToken cancellationToken);

        public delegate void TxBroadcaster(TPeer except, IEnumerable<Transaction> txs);

        internal AsyncAutoResetEvent TxReceived { get; }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource.Cancel();
                _disposed = true;
            }
        }

        public void Demand(TPeer peer, TxId txId) => Demand(peer, new[] { txId });

        public void Demand(TPeer peer, IEnumerable<TxId> txIds)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TxCompletion<TPeer>));
            }

            HashSet<TxId> required = GetRequiredTxIds(txIds);

            _logger.Information(
                "There are {RequiredCount} unaware transactions to receive out of {Count} TxIds",
                required.Count,
                txIds.Count());

            if (!required.Any())
            {
                return;
            }

            do
            {
                TxFetchJob txFetchJob = _txFetchJobs.GetOrAdd(
                    peer,
                    peerAsKey => TxFetchJob.RunAfter(
                        peerAsKey,
                        _txFetcher,
                        TimeSpan.FromSeconds(1),
                        (task) =>
                        {
                            if (task.IsCompleted &&
                                !task.IsFaulted &&
                                !task.IsCanceled &&
                                task.Result is ISet<Transaction> txs)
                            {
                                ProcessFetchedTxIds(txs, peerAsKey);
                            }

                            _txFetchJobs.TryRemove(peer, out _);
                        },
                        _cancellationTokenSource.Token));

                if (txFetchJob.TryAdd(required, out HashSet<TxId> rest))
                {
                    break;
                }

                required = rest;
                _txFetchJobs.TryRemove(peer, out _);
            }
            while (true);
        }

        private void ProcessFetchedTxIds(ISet<Transaction> txs, TPeer peer)
        {
            try
            {
                var policyCompatTxs = new HashSet<Transaction>(
                    txs.Where(
                        tx =>
                        {
                            try
                            {
                                _blockChain.Options.ValidateTransaction(_blockChain, tx);
                                return true;
                            }
                            catch
                            {
                                _blockChain.StagedTransactions.Ignore(tx.Id);
                                return false;
                            }
                        }));

                var stagedTxs = new List<Transaction>();
                foreach (var tx in policyCompatTxs)
                {
                    try
                    {
                        if (_blockChain.StagedTransactions.Add(tx))
                        {
                            stagedTxs.Add(tx);
                        }
                    }
                    catch (InvalidOperationException ite)
                    {
                        const string msg = "Received transaction from {Peer} with id {TxId} " +
                                  "will not be staged since it is invalid";
                        _logger.Error(ite, msg, peer, tx.Id);
                    }
                }

                // To maintain the consistency of the unit tests.
                if (policyCompatTxs.Any())
                {
                    TxReceived.Set();
                }

                if (stagedTxs.Any())
                {
                    _logger.Information(
                        "Staged {StagedTxCount} out of {TxCount} txs from {Peer}",
                        stagedTxs.Count,
                        txs.Count,
                        peer);

                    _txBroadcaster(peer, stagedTxs);
                }
                else
                {
                    _logger.Information(
                        "No transaction has been staged among received {TxCount} from {Peer}",
                        txs.Count,
                        peer);
                }
            }
            catch (Exception e)
            {
                _logger.Error(
                    e,
                    "An error occurred during {MethodName}() from {Peer}",
                    nameof(ProcessFetchedTxIds),
                    peer);
                throw;
            }
            finally
            {
                _logger.Debug(
                    "End of {MethodName}() from {Peer}",
                    nameof(ProcessFetchedTxIds),
                    peer);
            }
        }

        private HashSet<TxId> GetRequiredTxIds(IEnumerable<TxId> ids)
        {
            return new HashSet<TxId>(ids
                .Where(txId =>
                    _blockChain.StagedTransactions.ContainsKey(txId)
                        && _blockChain.StagedTransactions[txId] is null
                        && _blockChain.Store.PendingTransactions[txId] is null));
        }

        private class TxFetchJob
        {
            private readonly TxFetcher _txFetcher;
            private readonly Channel<TxId> _txIds;
            private readonly TPeer _peer;
            private readonly ILogger _logger;
            private readonly ReaderWriterLockSlim _txIdsWriterLock;

            private TxFetchJob(TxFetcher txFetcher, TPeer peer)
            {
                _txFetcher = txFetcher;
                _peer = peer;
                _txIds = Channel.CreateUnbounded<TxId>(
                    new UnboundedChannelOptions
                    {
                        SingleReader = true,
                    });
                _txIdsWriterLock = new ReaderWriterLockSlim();

                _logger = Log
                    .ForContext<TxFetchJob>()
                    .ForContext("Source", nameof(TxFetchJob));
            }

            public static TxFetchJob RunAfter(
                TPeer peer,
                TxFetcher txFetcher,
                TimeSpan waitFor,
                Action<Task<ISet<Transaction>>> continuation,
                CancellationToken cancellationToken)
            {
                var task = new TxFetchJob(txFetcher, peer);
                _ = task.RequestAsync(waitFor, cancellationToken).ContinueWith(continuation);
                return task;
            }

            public bool TryAdd(IEnumerable<TxId> txIds, out HashSet<TxId> rest)
            {
                rest = new HashSet<TxId>(txIds);
                _txIdsWriterLock.EnterReadLock();
                try
                {
                    foreach (TxId txId in txIds)
                    {
                        _txIds.Writer.WriteAsync(txId);
                        rest.Remove(txId);
                    }

                    return true;
                }
                catch (ChannelClosedException)
                {
                    return false;
                }
                finally
                {
                    _txIdsWriterLock.ExitReadLock();
                }
            }

            private async Task<ISet<Transaction>> RequestAsync(
                TimeSpan waitFor,
                CancellationToken cancellationToken)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(waitFor);
                    _txIdsWriterLock.EnterWriteLock();
                    try
                    {
                        _txIds.Writer.TryComplete();
                    }
                    finally
                    {
                        _txIdsWriterLock.ExitWriteLock();
                    }
                });

                try
                {
                    var txIds = new HashSet<TxId>();

                    while (await _txIds.Reader.WaitToReadAsync(cancellationToken))
                    {
                        while (_txIds.Reader.TryRead(out TxId txId))
                        {
                            txIds.Add(txId);
                        }
                    }

                    _logger.Debug(
                        "Start to run _txFetcher from {Peer}. (count: {Count})",
                        _peer,
                        txIds.Count);
                    var stopWatch = new Stopwatch();
                    stopWatch.Start();
                    var txs = new HashSet<Transaction>(
                        await _txFetcher(
                                _peer,
                                txIds,
                                cancellationToken)
                            .ToListAsync(cancellationToken)
                            .AsTask());
                    _logger.Debug(
                        "End of _txFetcher from {Peer}. (received: {Count}); " +
                        "Time taken: {Elapsed}",
                        _peer,
                        txs.Count,
                        stopWatch.Elapsed);

                    return txs;
                }
                catch (Exception e)
                {
                    _logger.Error(
                        e,
                        "An error occurred during {MethodName}() from {Peer}",
                        nameof(RequestAsync),
                        _peer);
                    throw;
                }
                finally
                {
                    _logger.Debug(
                        "End of {MethodName}() from {Peer}",
                        nameof(RequestAsync),
                        _peer);
                }
            }
        }
    }
}
