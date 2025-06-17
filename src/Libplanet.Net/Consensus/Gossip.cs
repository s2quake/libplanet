using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Protocols;
using Libplanet.Net.Transports;
using Serilog;

namespace Libplanet.Net.Consensus;

public sealed class Gossip : IAsyncDisposable
{
    private const int DLazy = 6;
    private readonly TimeSpan _rebuildTableInterval = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _refreshTableInterval = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _refreshLifespan = TimeSpan.FromSeconds(60);
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(1);
    private readonly ITransport _transport;
    private readonly MessageCache _cache;
    private readonly Action<MessageEnvelope> _validateMessageToReceive;
    private readonly Action<IMessage> _validateMessageToSend;
    private readonly Action<IMessage> _processMessage;
    private readonly IEnumerable<Peer> _seeds;

    private CancellationTokenSource? _cancellationTokenSource;
    private readonly RoutingTable _table;
    private readonly HashSet<Peer> _denySet;
    private readonly Kademlia _kademlia;
    private ConcurrentDictionary<Peer, HashSet<MessageId>> _haveDict;

    public Gossip(
        ITransport transport,
        ImmutableArray<Peer> peers,
        ImmutableArray<Peer> seeds,
        Action<MessageEnvelope> validateMessageToReceive,
        Action<IMessage> validateMessageToSend,
        Action<IMessage> processMessage)
    {
        _transport = transport;
        _cache = new MessageCache();
        _validateMessageToReceive = validateMessageToReceive;
        _validateMessageToSend = validateMessageToSend;
        _processMessage = processMessage;
        _table = new RoutingTable(transport.Peer.Address);

        // FIXME: Dumb way to add peer.
        foreach (Peer peer in peers.Where(p => p.Address != transport.Peer.Address))
        {
            _table.AddPeer(peer);
        }

        _kademlia = new Kademlia(_table, _transport, transport.Peer.Address);
        _seeds = seeds;

        _haveDict = new ConcurrentDictionary<Peer, HashSet<MessageId>>();
        _denySet = new HashSet<Peer>();
        IsRunning = false;
    }

    public bool IsRunning { get; private set; }

    public Peer AsPeer => _transport.Peer;

    public IEnumerable<Peer> Peers => _table.Peers;

    public IEnumerable<Peer> DeniedPeers => _denySet.ToList();

    public async Task StartAsync(CancellationToken ctx)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ctx);
        await _transport.StartAsync(ctx);

        try
        {
            await _kademlia.BootstrapAsync(_seeds, 3, ctx);
        }
        catch (InvalidOperationException pde)
        {
            // do noghing
        }

        _transport.MessageReceived.Subscribe(HandleMessage);
        IsRunning = true;
        await Task.WhenAny(
            RefreshTableAsync(_cancellationTokenSource.Token),
            RebuildTableAsync(_cancellationTokenSource.Token),
            HeartbeatTask(_cancellationTokenSource.Token));
    }

    public async Task StopAsync(CancellationToken ctx)
    {
        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
        }

        await _transport.StopAsync(ctx);
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _cache.Clear();
        await _transport.DisposeAsync();
    }

    public void PublishMessage(IMessage content) => PublishMessage(
        content,
        PeersToBroadcast(_table.Peers, DLazy));

    public void PublishMessage(IMessage content, IEnumerable<Peer> targetPeers)
    {
        AddMessage(content);
        _transport.BroadcastMessage(targetPeers, content);
    }

    public void AddMessage(IMessage content)
    {
        try
        {
            _cache.Put(content);
        }
        catch (ArgumentException)
        {
            return;
        }
        catch (Exception)
        {
            return;
        }

        try
        {
            _processMessage(content);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    public void AddMessages(IEnumerable<MessageBase> contents)
    {
        contents.AsParallel().ForAll(AddMessage);
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

    private IEnumerable<Peer> PeersToBroadcast(
        IEnumerable<Peer> peers,
        int count)
    {
        var rnd = new Random();
        return peers
            .Where(x => !_seeds.Contains(x))
            .OrderBy(x => rnd.Next())
            .Take(count);
    }

    private void HandleMessage(MessageEnvelope msg)
    {
        if (_denySet.Contains(msg.Peer))
        {
            ReplyMessagePong(msg);
            return;
        }

        try
        {
            _validateMessageToReceive(msg);
        }
        catch (Exception e)
        {
            return;
        }

        switch (msg.Message)
        {
            case PingMessage _:
            case FindNeighborsMessage _:
                // Ignore protocol related messages, Kadmelia Protocol will handle it.
                break;
            case HaveMessage _:
                HandleHave(msg);
                break;
            case WantMessage _:
                HandleWant(msg);
                break;
            default:
                ReplyMessagePong(msg);
                AddMessage(msg.Message);
                break;
        }
    }

    private async Task HeartbeatTask(CancellationToken ctx)
    {
        while (!ctx.IsCancellationRequested)
        {
            MessageId[] ids = _cache.GetGossipIds();
            if (ids.Any())
            {
                _transport.BroadcastMessage(
                    PeersToBroadcast(_table.Peers, DLazy),
                    new HaveMessage { Ids = [.. ids] });
            }

            _ = SendWantAsync(ctx);
            await Task.Delay(_heartbeatInterval, ctx);
        }
    }

    private void HandleHave(MessageEnvelope msg)
    {
        var haveMessage = (HaveMessage)msg.Message;

        ReplyMessagePong(msg);
        MessageId[] idsToGet = _cache.DiffFrom(haveMessage.Ids);

        if (!idsToGet.Any())
        {
            return;
        }

        if (!_haveDict.ContainsKey(msg.Peer))
        {
            _haveDict.TryAdd(msg.Peer, new HashSet<MessageId>(idsToGet));
        }
        else
        {
            List<MessageId> list = _haveDict[msg.Peer].ToList();
            list.AddRange(idsToGet.Where(id => !list.Contains(id)));
            _haveDict[msg.Peer] = new HashSet<MessageId>(list);
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

    private void HandleWant(MessageEnvelope msg)
    {
        // FIXME: Message may have been discarded.
        var wantMessage = (WantMessage)msg.Message;
        IMessage[] contents = wantMessage.Ids.Select(id => _cache.Get(id)).ToArray();
        MessageId[] ids = contents.Select(c => c.Id).ToArray();

        Parallel.ForEach(
            contents,
            async (c, cancellationToken) =>
            {
                try
                {
                    _validateMessageToSend(c);
                    _transport.ReplyMessage(msg.Identity, c);
                }
                catch (Exception e)
                {
                    // do nothing
                }
            });
    }

    private async Task RebuildTableAsync(CancellationToken ctx)
    {
        while (!ctx.IsCancellationRequested)
        {
            await Task.Delay(_rebuildTableInterval, ctx);
            try
            {
                await _kademlia.BootstrapAsync(
                    _seeds,
                    Kademlia.MaxDepth,
                    ctx);
            }
            catch (Exception e)
            {
                // do nothing
            }
        }
    }

    private async Task RefreshTableAsync(CancellationToken ctx)
    {
        while (!ctx.IsCancellationRequested)
        {
            try
            {
                await _kademlia.RefreshTableAsync(_refreshLifespan, ctx);
                await _kademlia.CheckReplacementCacheAsync(ctx);
                await Task.Delay(_refreshTableInterval, ctx);
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

    private void ReplyMessagePong(MessageEnvelope message)
    {
        _transport.ReplyMessage(message.Identity, new PongMessage());
    }
}
