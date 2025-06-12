using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Dasync.Collections;
using Libplanet.Net.Messages;
using Libplanet.Net.Protocols;
using Libplanet.Net.Transports;
using Serilog;

namespace Libplanet.Net.Consensus;

public sealed class Gossip : IDisposable
{
    private const int DLazy = 6;
    private readonly TimeSpan _rebuildTableInterval = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _refreshTableInterval = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _refreshLifespan = TimeSpan.FromSeconds(60);
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(1);
    private readonly ITransport _transport;
    private readonly MessageCache _cache;
    private readonly Action<MessageEnvelope> _validateMessageToReceive;
    private readonly Action<MessageContent> _validateMessageToSend;
    private readonly Action<MessageContent> _processMessage;
    private readonly IEnumerable<Peer> _seeds;

    private TaskCompletionSource<object?> _runningEvent;
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
        Action<MessageContent> validateMessageToSend,
        Action<MessageContent> processMessage)
    {
        _transport = transport;
        _cache = new MessageCache();
        _validateMessageToReceive = validateMessageToReceive;
        _validateMessageToSend = validateMessageToSend;
        _processMessage = processMessage;
        _table = new RoutingTable(transport.AsPeer.Address);

        // FIXME: Dumb way to add peer.
        foreach (Peer peer in peers.Where(p => p.Address != transport.AsPeer.Address))
        {
            _table.AddPeer(peer);
        }

        _kademlia = new Kademlia(_table, _transport, transport.AsPeer.Address);
        _seeds = seeds;

        _runningEvent = new TaskCompletionSource<object?>();
        _haveDict = new ConcurrentDictionary<Peer, HashSet<MessageId>>();
        _denySet = new HashSet<Peer>();
        Running = false;
    }

    public bool Running
    {
        get => _runningEvent.Task.Status == TaskStatus.RanToCompletion;

        private set
        {
            if (value)
            {
                _runningEvent.TrySetResult(null);
            }
            else
            {
                _runningEvent = new TaskCompletionSource<object?>();
            }
        }
    }

    public Peer AsPeer => _transport.AsPeer;

    public IEnumerable<Peer> Peers => _table.Peers;

    public IEnumerable<Peer> DeniedPeers => _denySet.ToList();

    public async Task StartAsync(CancellationToken ctx)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ctx);
        Task transportTask = _transport.StartAsync(ctx);
        await _transport.WaitForRunningAsync();
        try
        {
            await _kademlia.BootstrapAsync(_seeds, TimeSpan.FromSeconds(1), 3, ctx);
        }
        catch (PeerDiscoveryException pde)
        {
            // do noghing
        }

        _transport.ProcessMessageHandler.Register(HandleMessageAsync(_cancellationTokenSource.Token));
        Running = true;
        await Task.WhenAny(
            transportTask,
            RefreshTableAsync(_cancellationTokenSource.Token),
            RebuildTableAsync(_cancellationTokenSource.Token),
            HeartbeatTask(_cancellationTokenSource.Token));
    }

    public async Task StopAsync(TimeSpan waitFor, CancellationToken ctx)
    {
        _cancellationTokenSource?.Cancel();
        await _transport.StopAsync(waitFor, ctx);
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cache.Clear();
        _transport.Dispose();
    }

    public Task WaitForRunningAsync() => _runningEvent.Task;

    public void PublishMessage(MessageContent content) => PublishMessage(
        content,
        PeersToBroadcast(_table.Peers, DLazy));

    public void PublishMessage(MessageContent content, IEnumerable<Peer> targetPeers)
    {
        AddMessage(content);
        _transport.BroadcastMessage(targetPeers, content);
    }

    public void AddMessage(MessageContent content)
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

    public void AddMessages(IEnumerable<MessageContent> contents)
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

    private Func<MessageEnvelope, Task> HandleMessageAsync(CancellationToken ctx) => async msg =>
    {
        if (_denySet.Contains(msg.Remote))
        {
            await ReplyMessagePongAsync(msg, ctx);
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

        switch (msg.Content)
        {
            case PingMessage _:
            case FindNeighborsMessage _:
                // Ignore protocol related messages, Kadmelia Protocol will handle it.
                break;
            case HaveMessage _:
                await HandleHaveAsync(msg, ctx);
                break;
            case WantMessage _:
                await HandleWantAsync(msg, ctx);
                break;
            default:
                await ReplyMessagePongAsync(msg, ctx);
                AddMessage(msg.Content);
                break;
        }
    };

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

    private async Task HandleHaveAsync(MessageEnvelope msg, CancellationToken ctx)
    {
        var haveMessage = (HaveMessage)msg.Content;

        await ReplyMessagePongAsync(msg, ctx);
        MessageId[] idsToGet = _cache.DiffFrom(haveMessage.Ids);

        if (!idsToGet.Any())
        {
            return;
        }

        if (!_haveDict.ContainsKey(msg.Remote))
        {
            _haveDict.TryAdd(msg.Remote, new HashSet<MessageId>(idsToGet));
        }
        else
        {
            List<MessageId> list = _haveDict[msg.Remote].ToList();
            list.AddRange(idsToGet.Where(id => !list.Contains(id)));
            _haveDict[msg.Remote] = new HashSet<MessageId>(list);
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

        await optimized.ParallelForEachAsync(
            async pair =>
            {
                MessageId[] idsToGet = pair.Value;
                var want = new WantMessage { Ids = [.. idsToGet] };
                MessageEnvelope[] replies = (await _transport.SendMessageAsync(
                    pair.Key,
                    want,
                    TimeSpan.FromSeconds(1),
                    idsToGet.Length,
                    true,
                    ctx)).ToArray();

                replies.AsParallel().ForAll(
                    r =>
                    {
                        try
                        {
                            _validateMessageToReceive(r);
                            AddMessage(r.Content);
                        }
                        catch (Exception e)
                        {
                            // do nogthing
                        }
                    });
            },
            ctx);
    }

    private async Task HandleWantAsync(MessageEnvelope msg, CancellationToken ctx)
    {
        // FIXME: Message may have been discarded.
        var wantMessage = (WantMessage)msg.Content;
        MessageContent[] contents = wantMessage.Ids.Select(id => _cache.Get(id)).ToArray();
        MessageId[] ids = contents.Select(c => c.Id).ToArray();

        await contents.ParallelForEachAsync(
            async c =>
            {
                try
                {
                    _validateMessageToSend(c);
                    await _transport.ReplyMessageAsync(c, msg.Identity, ctx);
                }
                catch (Exception e)
                {
                    // do nothing
                }
            },
            ctx);
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
                    TimeSpan.FromSeconds(1),
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

    private async Task ReplyMessagePongAsync(MessageEnvelope message, CancellationToken ctx)
    {
        await _transport.ReplyMessageAsync(new PongMessage(), message.Identity, ctx);
    }
}
