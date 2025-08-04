using System.Reactive.Subjects;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Components;

public sealed class MessageBroadcaster : IAsyncDisposable
{
    private static readonly object _lock = new();
    private readonly Subject<(ImmutableArray<Peer>, ImmutableArray<MessageId>)> _broadcastedSubject = new();

    private readonly PeerExplorer _peerExplorer;
    private readonly HashSet<MessageId> _broadcastedMessageIds;
    private readonly DisposerCollection _subscriptions;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _broadcastingTask;
    private bool _disposed;

    public MessageBroadcaster(PeerExplorer peerExplorer, MessageCollection messages)
    {
        _subscriptions =
        [
            messages.Added.Subscribe(AddInternal),
            messages.Removed.Subscribe(RemoveInternal),
        ];
        _peerExplorer = peerExplorer;
        _broadcastingTask = BroadcastAsync();
        _broadcastedMessageIds = [.. messages.Select(m => m.Id)];
    }

    public IObservable<(ImmutableArray<Peer>, ImmutableArray<MessageId>)> Broadcasted => _broadcastedSubject;

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

    private void AddInternal(IMessage transaction)
    {
        lock (_lock)
        {
            _broadcastedMessageIds.Add(transaction.Id);
        }
    }

    private void RemoveInternal(IMessage transaction)
    {
        lock (_lock)
        {
            _broadcastedMessageIds.Remove(transaction.Id);
        }
    }

    private ImmutableArray<MessageId> FlushInternal()
    {
        lock (_lock)
        {
            var txIds = _broadcastedMessageIds.ToImmutableArray();
            _broadcastedMessageIds.Clear();
            return txIds;
        }
    }

    private async Task BroadcastAsync()
    {
        while (await TaskUtility.TryDelay(BroadcastInterval, _cancellationTokenSource.Token))
        {
            var messageIds = FlushInternal();
            if (messageIds.Length > 0)
            {
                var (peers, _) = _peerExplorer.BroadcastMessages(messageIds);
                _broadcastedSubject.OnNext((peers, messageIds));
            }
        }
    }
}
