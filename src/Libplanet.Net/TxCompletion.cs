// using System.Collections.Concurrent;
// using System.Reactive.Subjects;
// using System.Threading;
// using System.Threading.Tasks;
// using Libplanet.Types;
// using Libplanet.Types.Threading;
// using Nito.AsyncEx;

// namespace Libplanet.Net;

// public sealed class TxCompletion(
//     Blockchain blockchain,
//     TxCompletion.TxFetcher txFetcher,
//     TxCompletion.TxBroadcaster txBroadcaster)
//     : IDisposable
// {
//     private readonly CancellationTokenSource _cancellationTokenSource = new();
//     private readonly Blockchain _blockchain = blockchain;
//     private readonly TxFetcher _txFetcher = txFetcher;
//     private readonly TxBroadcaster _txBroadcaster = txBroadcaster;
//     private readonly ConcurrentDictionary<Peer, TxFetchJob> _txFetchJobs = new();
//     private readonly List<IDisposable> _subscriptionList = [];

//     private bool _disposed;

//     public delegate IAsyncEnumerable<Transaction> TxFetcher(
//         Peer peer,
//         IEnumerable<TxId> txIds,
//         CancellationToken cancellationToken);

//     public delegate void TxBroadcaster(Peer except, IEnumerable<Transaction> txs);

//     internal AsyncAutoResetEvent TxReceived { get; } = new AsyncAutoResetEvent();

//     public void Dispose()
//     {
//         if (!_disposed)
//         {
//             foreach (var subscription in _subscriptionList)
//             {
//                 subscription.Dispose();
//             }

//             _cancellationTokenSource.Cancel();
//             _cancellationTokenSource.Dispose();
//             _txFetchJobs.Clear();
//             _disposed = true;
//         }
//     }

//     public void Demand(Peer peer, TxId txId) => DemandMany(peer, [txId]);

//     public void DemandMany(Peer peer, IEnumerable<TxId> txIds)
//     {
//         ObjectDisposedException.ThrowIf(_disposed, this);

//         var requiredTxIds = GetRequiredTxIds(txIds);

//         if (_txFetchJobs.TryAdd(peer, new TxFetchJob(_txFetcher, peer)))
//         {
//             var fetcher = _txFetchJobs[peer];
//             var subscription = fetcher.Fetched.Subscribe(e => ProcessFetchedTxIds(e, peer));
//             _ = fetcher.RunAsync(_cancellationTokenSource.Token);
//             _subscriptionList.Add(subscription);
//         }

//         _txFetchJobs[peer].Add(requiredTxIds);
//     }

//     private void ProcessFetchedTxIds(HashSet<Transaction> txs, Peer peer)
//     {
//         var transactionOptions = _blockchain.Options.TransactionOptions;
//         var stageTransactions = _blockchain.StagedTransactions;
//         var txList = new List<Transaction>();

//         foreach (var tx in txs)
//         {
//             try
//             {
//                 transactionOptions.Validate(tx);
//                 if (!stageTransactions.ContainsKey(tx.Id))
//                 {
//                     stageTransactions.Add(tx);
//                     txList.Add(tx);
//                 }
//             }
//             catch
//             {
//                 stageTransactions.Remove(tx.Id);
//             }
//         }

//         if (txList.Count > 0)
//         {
//             TxReceived.Set();
//             _txBroadcaster(peer, txList);
//         }
//     }

//     private HashSet<TxId> GetRequiredTxIds(IEnumerable<TxId> txIds)
//     {
//         var stageTransactions = _blockchain.StagedTransactions;
//         var transactions = _blockchain.Transactions;
//         var query = from txId in txIds
//                     where stageTransactions.ContainsKey(txId) && !transactions.ContainsKey(txId)
//                     select txId;

//         return [.. query];
//     }

//     private sealed class TxFetchJob(TxFetcher txFetcher, Peer peer)
//     {
//         private static readonly object _lock = new();
//         private readonly List<TxId> _txIdList = [];
//         private readonly Subject<HashSet<Transaction>> _fetchedSubject = new();

//         public IObservable<HashSet<Transaction>> Fetched => _fetchedSubject;

//         public async Task RunAsync(CancellationToken cancellationToken)
//         {
//             do
//             {
//                 var txs = new HashSet<Transaction>();
//                 var txIds = Flush();
//                 await foreach (var item in txFetcher(peer, txIds, cancellationToken))
//                 {
//                     txs.Add(item);
//                 }

//                 if (txs.Count > 0)
//                 {
//                     _fetchedSubject.OnNext(txs);
//                 }
//             } while (await TaskUtility.TryDelay(1000, cancellationToken));
//         }

//         private TxId[] Flush()
//         {
//             lock (_lock)
//             {
//                 var txIds = _txIdList.ToArray();
//                 _txIdList.Clear();
//                 return txIds;
//             }
//         }

//         public void Add(IEnumerable<TxId> txIds)
//         {
//             lock (_lock)
//             {
//                 _txIdList.AddRange(txIds);
//             }
//         }
//     }
// }
