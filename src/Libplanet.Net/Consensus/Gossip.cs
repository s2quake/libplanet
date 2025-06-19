using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Protocols;

namespace Libplanet.Net.Consensus;

public sealed class Gossip : IAsyncDisposable
{
    private const int DLazy = 6;
    private readonly GossipOptions _options = new();
    private readonly ITransport _transport;
    private readonly ConcurrentDictionary<MessageId, IMessage> _messageById = new();
    private readonly Action<MessageEnvelope> _validateMessageToReceive;
    private readonly Action<IMessage> _validateMessageToSend;
    private readonly Action<IMessage> _processMessage;
    private readonly ImmutableArray<Peer> _seeds;
    private readonly RoutingTable _table;
    private readonly HashSet<Peer> _denySet = [];
    private readonly Kademlia _kademlia;
    private ConcurrentDictionary<Peer, HashSet<MessageId>> _haveDict;
    private CancellationTokenSource? _cancellationTokenSource;
    private IDisposable? _transportSubscription;
    private bool _disposed;

    public Gossip(ITransport transport)
        : this(transport, new GossipOptions())
    {
    }

    public Gossip(ITransport transport, GossipOptions options)
    {
        _transport = transport;
        _validateMessageToReceive = options.ValidateMessageToReceive;
        _validateMessageToSend = options.ValidateMessageToSend;
        _processMessage = options.ProcessMessage;
        _table = new RoutingTable(transport.Peer.Address);

        // FIXME: Dumb way to add peer.
        foreach (Peer peer in options.Validators.Where(p => p.Address != transport.Peer.Address))
        {
            _table.AddPeer(peer);
        }

        _kademlia = new Kademlia(_table, _transport, transport.Peer.Address);
        _seeds = options.Seeds;
        _haveDict = new ConcurrentDictionary<Peer, HashSet<MessageId>>();
    }

    public bool IsRunning { get; private set; }

    public Peer AsPeer => _transport.Peer;

    public ImmutableArray<Peer> Peers => _table.Peers;

    public IEnumerable<Peer> DeniedPeers => _denySet.ToList();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning)
        {
            throw new InvalidOperationException("Gossip is already running.");
        }

        _cancellationTokenSource = new CancellationTokenSource();
        await _transport.StartAsync(cancellationToken);
        _transportSubscription = _transport.MessageReceived.Subscribe(HandleMessage);
        await _kademlia.BootstrapAsync(_seeds, 3, cancellationToken);
        await Task.WhenAny(
            RefreshTableAsync(_cancellationTokenSource.Token),
            RebuildTableAsync(_cancellationTokenSource.Token),
            HeartbeatAsync(_cancellationTokenSource.Token));
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

        _transportSubscription?.Dispose();
        _transportSubscription = null;
        await _transport.StopAsync(cancellationToken);
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
        => PublishMessage(PeersToBroadcast(_table.Peers, DLazy), message);

    public void PublishMessage(IEnumerable<Peer> targetPeers, params IMessage[] messages)
    {
        foreach (var message in messages)
        {
            AddMessage(message);
            _transport.BroadcastMessage(targetPeers, message);
        }
    }

    public void AddMessage(IMessage message)
    {
        if (_messageById.TryAdd(message.Id, message))
        {
            try
            {
                _processMessage(message);
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    public void AddMessages(IEnumerable<IMessage> messages)
    {
        messages.AsParallel().ForAll(AddMessage);
    }

    public void DenyPeer(Peer peer)
    {
        _denySet.Add(peer);
    }

    public void AllowPeer(Peer peer)
    {
        _denySet.Remove(peer);
    }

    public void ClearDenySet()
    {
        _denySet.Clear();
    }

    private IEnumerable<Peer> PeersToBroadcast(IEnumerable<Peer> peers, int count)
    {
        var random = new Random();
        return peers
            .Where(x => !_seeds.Contains(x))
            .OrderBy(x => random.Next())
            .Take(count);
    }

    private void HandleMessage(MessageEnvelope messageEnvelope)
    {
        if (_denySet.Contains(messageEnvelope.Peer))
        {
            ReplyPongMessage(messageEnvelope);
            return;
        }

        try
        {
            _validateMessageToReceive(messageEnvelope);
        }
        catch
        {
            return;
        }

        switch (messageEnvelope.Message)
        {
            case PingMessage:
            case FindNeighborsMessage:
                // Ignore protocol related messages, Kadmelia Protocol will handle it.
                break;
            case HaveMessage:
                HandleHave(messageEnvelope);
                break;
            case WantMessage:
                HandleWant(messageEnvelope);
                break;
            default:
                ReplyPongMessage(messageEnvelope);
                AddMessage(messageEnvelope.Message);
                break;
        }
    }

    private async Task HeartbeatAsync(CancellationToken cancellationToken)
    {
        var interval = _options.HeartbeatInterval;
        while (!cancellationToken.IsCancellationRequested)
        {
            var ids = _messageById.Keys.ToArray();
            if (ids.Length > 0)
            {
                var peers = PeersToBroadcast(_table.Peers, DLazy);
                var message = new HaveMessage { Ids = [.. ids] };
                _transport.BroadcastMessage(peers, message);
            }

            _ = SendWantAsync(cancellationToken);
            await Task.Delay(interval, cancellationToken);
        }
    }

    private void HandleHave(MessageEnvelope messageEnvelope)
    {
        var haveMessage = (HaveMessage)messageEnvelope.Message;

        ReplyPongMessage(messageEnvelope);
        //             return ids.Where(id => !_messages.TryGetValue(id, out _)).ToArray();
        MessageId[] idsToGet = _messageById.Keys.Where(id => !_messageById.ContainsKey(id)).ToArray();

        if (!idsToGet.Any())
        {
            return;
        }

        if (!_haveDict.ContainsKey(messageEnvelope.Peer))
        {
            _haveDict.TryAdd(messageEnvelope.Peer, [.. idsToGet]);
        }
        else
        {
            List<MessageId> list = _haveDict[messageEnvelope.Peer].ToList();
            list.AddRange(idsToGet.Where(id => !list.Contains(id)));
            _haveDict[messageEnvelope.Peer] = [.. list];
        }
    }

    private async Task SendWantAsync(CancellationToken ctx)
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
            ctx,
            async (pair, cancellationToken) =>
            {
                MessageId[] idsToGet = pair.Value;
                var want = new WantMessage { Ids = [.. idsToGet] };
                MessageEnvelope replies = await _transport.SendMessageAsync(
                    pair.Key,
                    want,
                    cancellationToken);

                _validateMessageToReceive(replies);
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

    private void HandleWant(MessageEnvelope messageEnvelope)
    {
        // FIXME: Message may have been discarded.
        var wantMessage = (WantMessage)messageEnvelope.Message;
        IMessage[] contents = wantMessage.Ids.Select(id => _messageById[id]).ToArray();
        MessageId[] ids = contents.Select(c => c.Id).ToArray();

        Parallel.ForEach(
            contents,
            async (c, cancellationToken) =>
            {
                try
                {
                    _validateMessageToSend(c);
                    _transport.ReplyMessage(messageEnvelope.Identity, c);
                }
                catch (Exception e)
                {
                    // do nothing
                }
            });
    }

    private async Task RebuildTableAsync(CancellationToken cancellationToken)
    {
        var interval = _options.RebuildTableInterval;
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, cancellationToken);
            try
            {
                await _kademlia.BootstrapAsync(
                    _seeds,
                    Kademlia.MaxDepth,
                    cancellationToken);
            }
            catch (Exception e)
            {
                // do nothing
            }
        }
    }

    private async Task RefreshTableAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _kademlia.RefreshTableAsync(_options.RefreshLifespan, cancellationToken);
                await _kademlia.CheckReplacementCacheAsync(cancellationToken);
                await Task.Delay(_options.RefreshTableInterval, cancellationToken);
            }
            catch (OperationCanceledException e)
            {
                throw;
            }
            catch (Exception e)
            {
                // do nothing
            }
        }
    }

    private void ReplyPongMessage(MessageEnvelope messageEnvelope)
    {
        _transport.ReplyMessage(messageEnvelope.Identity, new PongMessage());
    }
}
