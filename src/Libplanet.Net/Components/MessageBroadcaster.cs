using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
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

    // public async Task HeartbeatAsync(CancellationToken cancellationToken)
    // {
    //     var peers = _peerExplorer?.Peers ?? throw new InvalidOperationException("Gossip is not running.");
    //     var ids = _messageById.Keys.ToArray();
    //     if (ids.Length > 0)
    //     {
    //         var peersToBroadcast = GetPeersToBroadcast(peers, DLazy);
    //         var message = new HaveMessage { Ids = [.. ids] };
    //         _transport.Post(peersToBroadcast, message);
    //     }

    //     await SendWantMessageAsync(cancellationToken);
    // }

    //  private async Task SendWantMessageAsync(CancellationToken cancellationToken)
    // {
    //     var copy = _haveDict.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    //     _haveDict = new ConcurrentDictionary<Peer, HashSet<MessageId>>();
    //     var optimized = new Dictionary<Peer, MessageId[]>();
    //     while (copy.Count > 0)
    //     {
    //         var longest = copy.OrderBy(pair => pair.Value.Length).Last();
    //         optimized.Add(longest.Key, longest.Value);
    //         copy.Remove(longest.Key);
    //         var removeCandidate = new List<Peer>();
    //         foreach (var pair in copy)
    //         {
    //             var clean = pair.Value.Where(id => !longest.Value.Contains(id)).ToArray();
    //             if (clean.Length > 0)
    //             {
    //                 copy[pair.Key] = clean;
    //             }
    //             else
    //             {
    //                 removeCandidate.Add(pair.Key);
    //             }
    //         }

    //         foreach (var peer in removeCandidate)
    //         {
    //             copy.Remove(peer);
    //         }
    //     }

    //     await Parallel.ForEachAsync(
    //         optimized,
    //         cancellationToken,
    //         async (pair, cancellationToken) =>
    //         {
    //             MessageId[] idsToGet = pair.Value;
    //             var wantMessage = new WantMessage { Ids = [.. idsToGet] };
    //             try
    //             {
    //                 var query = _transport.SendAsync<IMessage>(pair.Key, wantMessage, m => true, cancellationToken);
    //                 await foreach (var item in query)
    //                 {
    //                     _messageById.TryAdd(item.Id, item);
    //                     // Messagehandlers.HandleAsync(item)
    //                     // _validateReceivedMessageSubject.OnNext((pair.Key, item));
    //                     MessageValidators.Validate(item);
    //                 }
    //             }
    //             catch (Exception e)
    //             {

    //             }
    //         });
    // }

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
