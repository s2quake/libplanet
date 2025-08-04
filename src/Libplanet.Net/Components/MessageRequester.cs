using System.Reactive.Subjects;
using Libplanet.Net.Messages;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Components;

public sealed class MessageRequester : IAsyncDisposable
{
    private readonly Subject<(Peer, ImmutableArray<MessageId>)> _postedSubject = new();

    private readonly ITransport _transport;
    private readonly MessageCollection _messages;
    private readonly PeerMessageIdCollection _peerMessageIds;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _postingTask;
    private bool _disposed;

    public MessageRequester(
        ITransport transport, MessageCollection messages, PeerMessageIdCollection peerMessageIds)
    {
        _transport = transport;
        _messages = messages;
        _peerMessageIds = peerMessageIds;
        _postingTask = BroadcastAsync();
    }

    public IObservable<(Peer, ImmutableArray<MessageId>)> Posted => _postedSubject;

    public TimeSpan BroadcastInterval { get; set; } = TimeSpan.FromSeconds(1);

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _cancellationTokenSource.CancelAsync();
            await TaskUtility.TryWait(_postingTask);
            _cancellationTokenSource.Dispose();
            _postedSubject.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private async Task BroadcastAsync()
    {
        while (await TaskUtility.TryDelay(BroadcastInterval, _cancellationTokenSource.Token))
        {
            var items = _peerMessageIds.Flush(_messages);
            foreach (var item in items)
            {
                var peer = item.Item1;
                var messageIds = item.Item2;
                var message = new WantMessage { Ids = [.. messageIds] };
                _transport.Post(peer, message);
                _postedSubject.OnNext((peer, messageIds));
            }
        }
    }
}
