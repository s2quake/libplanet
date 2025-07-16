using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Protocols;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Consensus;

public sealed class Gossip(
    ITransport transport, ImmutableHashSet<Peer> seeds, ImmutableHashSet<Peer> validators, GossipOptions options)
    : ServiceBase
{
    private const int DLazy = 6;
    private readonly Subject<(Peer, IMessage)> _validateReceivedMessageSubject = new();
    private readonly Subject<IMessage> _validateSendingMessageSubject = new();
    private readonly Subject<IMessage> _processMessageSubject = new();
    private readonly ITransport _transport = transport;
    private readonly GossipOptions _options = options;
    private readonly ConcurrentDictionary<MessageId, IMessage> _messageById = new();
    private readonly HashSet<Peer> _deniedPeers = [];
    private RoutingTable? _table;
    private PeerDiscovery? peerDiscovery;
    private ConcurrentDictionary<Peer, HashSet<MessageId>> _haveDict = new();
    private Task[] _tasks = [];
    private IDisposable? _transportSubscription;

    public Gossip(ITransport transport)
        : this(transport, [], [], new GossipOptions())
    {
    }

    public IObservable<(Peer, IMessage)> ValidateReceivedMessage => _validateReceivedMessageSubject;

    public IObservable<IMessage> ValidateSendingMessage => _validateSendingMessageSubject;

    public IObservable<IMessage> ProcessMessage => _processMessageSubject;

    public Peer Peer => _transport.Peer;

    public ImmutableArray<Peer> Peers
    {
        get
        {
            if (_table is null)
            {
                throw new InvalidOperationException("Gossip is not running.");
            }

            return [.. _table.Select(item => item.Peer)];
        }
    }

    public ImmutableArray<Peer> DeniedPeers => [.. _deniedPeers];

    public void ClearCache()
    {
        _messageById.Clear();
    }

    public void PublishMessage(IMessage message)
    {
        if (_table is null)
        {
            throw new InvalidOperationException("Gossip is not running.");
        }

        PublishMessage(GetPeersToBroadcast(_table.Select(item => item.Peer), DLazy), message);
    }

    public void PublishMessage(ImmutableArray<Peer> targetPeers, params IMessage[] messages)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!IsRunning)
        {
            throw new InvalidOperationException("Gossip is not running.");
        }

        foreach (var message in messages)
        {
            AddMessage(message);
            _transport.Send(targetPeers, message);
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

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        await _transport.StartAsync(cancellationToken);
        _table = new RoutingTable(_transport.Peer.Address);
        _table.AddRange(validators);
        _transportSubscription = _transport.Process.Subscribe(HandleMessage);
        peerDiscovery = new PeerDiscovery(_table, _transport);
        await peerDiscovery.BootstrapAsync(seeds, 3, cancellationToken);
        _tasks =
        [
            RunTableRefreshAsync(),
            RunTableRebuildAsync(),
            RunHeartbeatAsync()
        ];
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        await TaskUtility.TryWhenAll(_tasks);
        _tasks = [];
        _transportSubscription?.Dispose();
        _transportSubscription = null;
        await _transport.StopAsync(cancellationToken);
        _table = null;
        peerDiscovery = null;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _transportSubscription?.Dispose();
        _transportSubscription = null;

        // Subject들을 정리
        _validateReceivedMessageSubject.Dispose();
        _validateSendingMessageSubject.Dispose();
        _processMessageSubject.Dispose();

        _messageById.Clear();
        await _transport.DisposeAsync();
        await base.DisposeAsyncCore();
    }

    private ImmutableArray<Peer> GetPeersToBroadcast(IEnumerable<Peer> peers, int count)
    {
        var random = new Random();
        var query = from peer in peers
                    where !seeds.Contains(peer)
                    orderby random.Next()
                    select peer;

        return [.. query.Take(count)];
    }

    private void HandleMessage(IReplyContext replyContext)
    {
        if (_deniedPeers.Contains(replyContext.Sender))
        {
            replyContext.PongAsync();
            return;
        }

        try
        {
            _validateReceivedMessageSubject.OnNext((replyContext.Sender, replyContext.Message));
        }
        catch (Exception ex)
        {
            // 유효성 검사 실패 시 로깅하고 메시지 처리 중단
            // TODO: 로거 추가 시 여기서 로깅
            return;
        }

        switch (replyContext.Message)
        {
            case PingMessage:
            case GetPeerMessage:
                break;
            case HaveMessage:
                replyContext.PongAsync();
                HandleHaveMessage(replyContext);
                break;
            case WantMessage:
                HandleWantMessage(replyContext);
                break;
            default:
                replyContext.PongAsync();
                AddMessage(replyContext.Message);
                break;
        }
    }

    private async Task RunHeartbeatAsync()
    {
        using var cancellationTokenSource = CreateCancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var table = _table ?? throw new InvalidOperationException("Gossip is not running.");
        var interval = _options.HeartbeatInterval;
        while (!cancellationToken.IsCancellationRequested)
        {
            var ids = _messageById.Keys.ToArray();
            if (ids.Length > 0)
            {
                var peers = GetPeersToBroadcast(table.Select(item => item.Peer), DLazy);
                var message = new HaveMessage { Ids = [.. ids] };
                _transport.Send(peers, message);
            }

            _ = SendWantMessageAsync(cancellationToken);
            await Task.Delay(interval, cancellationToken);
        }
    }

    private void HandleHaveMessage(IReplyContext messageEnvelope)
    {
        var haveMessage = (HaveMessage)messageEnvelope.Message;
        var ids = haveMessage.Ids.Where(id => !_messageById.ContainsKey(id)).ToArray();
        if (ids.Length is 0)
        {
            return;
        }

        var peer = messageEnvelope.Sender;
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
        var copy = _haveDict.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        _haveDict = new ConcurrentDictionary<Peer, HashSet<MessageId>>();
        var optimized = new Dictionary<Peer, MessageId[]>();
        while (copy.Count > 0)
        {
            var longest = copy.OrderBy(pair => pair.Value.Length).Last();
            optimized.Add(longest.Key, longest.Value);
            copy.Remove(longest.Key);
            var removeCandidate = new List<Peer>();
            foreach (var pair in copy)
            {
                var clean = pair.Value.Where(id => !longest.Value.Contains(id)).ToArray();
                if (clean.Length > 0)
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
                await foreach (var item in _transport.SendAsync(pair.Key, want, cancellationToken))
                {
                    try
                    {
                        AddMessage(item);
                    }
                    catch
                    {
                        // do nogthing
                    }
                    _validateReceivedMessageSubject.OnNext((pair.Key, item));
                }
            });
    }

    private void HandleWantMessage(IReplyContext replyContext)
    {
        var wantMessage = (WantMessage)replyContext.Message;
        var messages = wantMessage.Ids.Select(id => _messageById[id]).ToArray();

        Parallel.ForEach(messages, Invoke);

        void Invoke(IMessage message)
        {
            try
            {
                _validateSendingMessageSubject.OnNext(message);
                replyContext.NextAsync(message);
            }
            catch (Exception)
            {
                // do nothing
            }
        }
    }

    private async Task RunTableRebuildAsync()
    {
        using var cancellationTokenSource = CreateCancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var kademlia = peerDiscovery ?? throw new InvalidOperationException("Gossip is not running.");
        var interval = _options.RebuildTableInterval;
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, cancellationToken);
            try
            {
                await kademlia.BootstrapAsync(seeds, PeerDiscovery.MaxDepth, cancellationToken);
            }
            catch
            {
                // do nothing
            }
        }
    }

    private async Task RunTableRefreshAsync()
    {
        using var cancellationTokenSource = CreateCancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var kademlia = peerDiscovery ?? throw new InvalidOperationException("Gossip is not running.");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await kademlia.RefreshPeersAsync(_options.RefreshLifespan, cancellationToken);
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
