using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Protocols;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Consensus;

public sealed class Gossip : IAsyncDisposable
{
    private const int DLazy = 6;
    private readonly Subject<MessageEnvelope> _validateReceivedMessageSubject = new();
    private readonly Subject<IMessage> _validateSendingMessageSubject = new();
    private readonly Subject<IMessage> _processMessageSubject = new();
    private readonly ITransport _transport;
    private readonly GossipOptions _options;
    private readonly ConcurrentDictionary<MessageId, IMessage> _messageById = new();
    private readonly HashSet<Peer> _deniedPeers = [];
    private RoutingTable? _table;
    private Kademlia? _kademlia;
    private ConcurrentDictionary<Peer, HashSet<MessageId>> _haveDict;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task[] _tasks = [];
    private IDisposable? _transportSubscription;
    private bool _disposed;

    public Gossip(ITransport transport)
        : this(transport, new GossipOptions())
    {
    }

    public Gossip(ITransport transport, GossipOptions options)
    {
        _transport = transport;
        _options = options;
        _haveDict = new ConcurrentDictionary<Peer, HashSet<MessageId>>();
    }

    public IObservable<MessageEnvelope> ValidateReceivedMessage => _validateReceivedMessageSubject;

    public IObservable<IMessage> ValidateSendingMessage => _validateSendingMessageSubject;

    public IObservable<IMessage> ProcessMessage => _processMessageSubject;

    public bool IsRunning { get; private set; }

    public Peer Peer => _transport.Peer;

    public ImmutableArray<Peer> Peers => _table?.Peers ?? throw new InvalidOperationException("Gossip is not running.");

    public ImmutableArray<Peer> DeniedPeers => [.. _deniedPeers];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning)
        {
            throw new InvalidOperationException("Gossip is already running.");
        }

        _cancellationTokenSource = new CancellationTokenSource();
        await _transport.StartAsync(cancellationToken);
        _table = new RoutingTable(_transport.Peer.Address);
        _table.AddPeers(_options.Validators);
        _transportSubscription = _transport.ProcessMessage.Subscribe(HandleMessage);
        _kademlia = new Kademlia(_table, _transport, _transport.Peer.Address);
        await _kademlia.BootstrapAsync(_options.Seeds, 3, cancellationToken);
        _tasks =
        [
            RunTableRefreshAsync(_cancellationTokenSource.Token),
            RunTableRebuildAsync(_cancellationTokenSource.Token),
            RunHeartbeatAsync(_cancellationTokenSource.Token)
        ];
        IsRunning = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsRunning)
        {
            throw new InvalidOperationException("Gossip is not running.");
        }

        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
        }

        await TaskUtility.TryWaitAll(_tasks);
        _tasks = [];
        _transportSubscription?.Dispose();
        _transportSubscription = null;
        await _transport.StopAsync(cancellationToken);
        _table = null;
        _kademlia = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        IsRunning = false;
    }

    public void ClearCache()
    {
        _messageById.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync();
            }

            _transportSubscription?.Dispose();
            _transportSubscription = null;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _messageById.Clear();
            await _transport.DisposeAsync();
            _disposed = true;
        }
    }

    public void PublishMessage(IMessage message)
    {
        if (_table is null)
        {
            throw new InvalidOperationException("Gossip is not running.");
        }

        PublishMessage(GetPeersToBroadcast(_table.Peers, DLazy), message);
    }

    public void PublishMessage(IEnumerable<Peer> targetPeers, params IMessage[] messages)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsRunning)
        {
            throw new InvalidOperationException("Gossip is not running.");
        }

        foreach (var message in messages)
        {
            AddMessage(message);
            _transport.BroadcastMessage(targetPeers, message);
        }
    }

    private void AddMessage(IMessage message)
    {
        if (_messageById.TryAdd(message.Id, message))
        {
            try
            {
                _processMessageSubject.OnNext(message);
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    public void DenyPeer(Peer peer)
    {
        _deniedPeers.Add(peer);
    }

    public void AllowPeer(Peer peer)
    {
        _deniedPeers.Remove(peer);
    }

    public void ClearDenySet()
    {
        _deniedPeers.Clear();
    }

    private IEnumerable<Peer> GetPeersToBroadcast(IEnumerable<Peer> peers, int count)
    {
        var random = new Random();
        var query = from peer in peers
                    where !_options.Seeds.Contains(peer)
                    orderby random.Next()
                    select peer;

        return query.Take(count);
    }

    private void HandleMessage(MessageEnvelope messageEnvelope)
    {
        if (_deniedPeers.Contains(messageEnvelope.Peer))
        {
            _transport.Pong(messageEnvelope);
            return;
        }

        try
        {
            _validateReceivedMessageSubject.OnNext(messageEnvelope);
        }
        catch
        {
            return;
        }

        switch (messageEnvelope.Message)
        {
            case PingMessage:
            case FindNeighborsMessage:
                break;
            case HaveMessage:
                _transport.Pong(messageEnvelope);
                HandleHaveMessage(messageEnvelope);
                break;
            case WantMessage:
                HandleWantMessage(messageEnvelope);
                break;
            default:
                _transport.Pong(messageEnvelope);
                AddMessage(messageEnvelope.Message);
                break;
        }
    }

    private async Task RunHeartbeatAsync(CancellationToken cancellationToken)
    {
        var table = _table ?? throw new InvalidOperationException("Gossip is not running.");
        var interval = _options.HeartbeatInterval;
        while (!cancellationToken.IsCancellationRequested)
        {
            var ids = _messageById.Keys.ToArray();
            if (ids.Length > 0)
            {
                var peers = GetPeersToBroadcast(table.Peers, DLazy);
                var message = new HaveMessage { Ids = [.. ids] };
                _transport.BroadcastMessage(peers, message);
            }

            _ = SendWantMessageAsync(cancellationToken);
            await Task.Delay(interval, cancellationToken);
        }
    }

    private void HandleHaveMessage(MessageEnvelope messageEnvelope)
    {
        var haveMessage = (HaveMessage)messageEnvelope.Message;
        var ids = haveMessage.Ids.Where(id => !_messageById.ContainsKey(id)).ToArray();
        if (ids.Length is 0)
        {
            return;
        }

        var peer = messageEnvelope.Peer;
        if (!_haveDict.TryGetValue(peer, out HashSet<MessageId>? value))
        {
            value = [];
        }

        foreach (var id in ids)
        {
            value.Add(id);
        }

        _haveDict[peer] = value;
    }

    private async Task SendWantMessageAsync(CancellationToken cancellationToken)
    {
        // TODO: To optimize WantMessage count to minimum, should remove duplications.
        var copy = _haveDict.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        _haveDict = new ConcurrentDictionary<Peer, HashSet<MessageId>>();
        var optimized = new Dictionary<Peer, MessageId[]>();
        while (copy.Any())
        {
            var longest = copy.OrderBy(pair => pair.Value.Length).Last();
            optimized.Add(longest.Key, longest.Value);
            copy.Remove(longest.Key);
            var removeCandidate = new List<Peer>();
            foreach (var pair in copy)
            {
                var clean = pair.Value.Where(id => !longest.Value.Contains(id)).ToArray();
                if (clean.Any())
                {
                    copy[pair.Key] = clean;
                }
                else
                {
                    removeCandidate.Add(pair.Key);
                }
            }

            foreach (var peer in removeCandidate)
            {
                copy.Remove(peer);
            }
        }

        await Parallel.ForEachAsync(
            optimized,
            cancellationToken,
            async (pair, cancellationToken) =>
            {
                MessageId[] idsToGet = pair.Value;
                var want = new WantMessage { Ids = [.. idsToGet] };
                MessageEnvelope replies = await _transport.SendMessageAsync(
                    pair.Key,
                    want,
                    cancellationToken);

                _validateReceivedMessageSubject.OnNext(replies);
                var message = (AggregateMessage)replies.Message;

                message.Messages.AsParallel().ForAll(
                    r =>
                    {
                        try
                        {

                            AddMessage(r);
                        }
                        catch (Exception e)
                        {
                            // do nogthing
                        }
                    });
            });
    }

    private void HandleWantMessage(MessageEnvelope messageEnvelope)
    {
        var wantMessage = (WantMessage)messageEnvelope.Message;
        var contents = wantMessage.Ids.Select(id => _messageById[id]).ToArray();

        Parallel.ForEach(contents, Invoke);

        void Invoke(IMessage message)
        {
            try
            {
                _validateSendingMessageSubject.OnNext(message);
                _transport.ReplyMessage(messageEnvelope.Identity, message);
            }
            catch (Exception)
            {
                // do nothing
            }
        }
    }

    private async Task RunTableRebuildAsync(CancellationToken cancellationToken)
    {
        var kademlia = _kademlia ?? throw new InvalidOperationException("Gossip is not running.");
        var interval = _options.RebuildTableInterval;
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, cancellationToken);
            try
            {
                await kademlia.BootstrapAsync(_options.Seeds, Kademlia.MaxDepth, cancellationToken);
            }
            catch
            {
                // do nothing
            }
        }
    }

    private async Task RunTableRefreshAsync(CancellationToken cancellationToken)
    {
        var kademlia = _kademlia ?? throw new InvalidOperationException("Gossip is not running.");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await kademlia.RefreshTableAsync(_options.RefreshLifespan, cancellationToken);
                await kademlia.CheckReplacementCacheAsync(cancellationToken);
                await Task.Delay(_options.RefreshTableInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // do nothing
            }
        }
    }
}
