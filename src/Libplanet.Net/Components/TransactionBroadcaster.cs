using System.Reactive.Subjects;
using Libplanet.Types;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Components;

public sealed class TransactionBroadcaster : IAsyncDisposable
{
    private static readonly object _lock = new();
    private readonly Subject<(ImmutableArray<Peer>, ImmutableArray<TxId>)> _broadcastedSubject = new();

    private readonly PeerExplorer _peerExplorer;
    private readonly HashSet<TxId> _broadcastedTxIds;
    private readonly DisposerCollection _subscriptions;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _broadcastingTask;
    private bool _disposed;

    public TransactionBroadcaster(Blockchain blockchain, PeerExplorer peerExplorer)
    {
        _subscriptions =
        [
            blockchain.StagedTransactions.Added.Subscribe(AddInternal),
            blockchain.StagedTransactions.Removed.Subscribe(RemoveInternal),
        ];
        _peerExplorer = peerExplorer;
        _broadcastingTask = BroadcastAsync();
        _broadcastedTxIds = [.. blockchain.StagedTransactions.Keys];
    }

    public IObservable<(ImmutableArray<Peer>, ImmutableArray<TxId>)> Broadcasted => _broadcastedSubject;

    public TimeSpan BroadcastInterval { get; set; } = TimeSpan.FromSeconds(1);

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _cancellationTokenSource.CancelAsync();
            await TaskUtility.TryWait(_broadcastingTask);
            _cancellationTokenSource.Dispose();
            _subscriptions.Dispose();
            _broadcastedSubject.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private void AddInternal(Transaction transaction)
    {
        lock (_lock)
        {
            _broadcastedTxIds.Add(transaction.Id);
        }
    }

    private void RemoveInternal(Transaction transaction)
    {
        lock (_lock)
        {
            _broadcastedTxIds.Remove(transaction.Id);
        }
    }

    private ImmutableArray<TxId> FlushInternal()
    {
        lock (_lock)
        {
            var txIds = _broadcastedTxIds.ToImmutableArray();
            _broadcastedTxIds.Clear();
            return txIds;
        }
    }

    private async Task BroadcastAsync()
    {
        while (await TaskUtility.TryDelay(BroadcastInterval, _cancellationTokenSource.Token))
        {
            var txIds = FlushInternal();
            if (txIds.Length > 0)
            {
                var (peers, _) = _peerExplorer.BroadcastTransaction(txIds);
                _broadcastedSubject.OnNext((peers, txIds));
            }
        }
    }
}
