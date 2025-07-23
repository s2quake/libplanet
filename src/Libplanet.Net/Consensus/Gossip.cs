using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus.GossipMessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Net.Protocols;
using Libplanet.Net.Tasks;

namespace Libplanet.Net.Consensus;

public sealed class Gossip : ServiceBase
{
    private const int DLazy = 6;
    private readonly ITransport _transport;
    private readonly GossipOptions _options;
    private readonly ConcurrentDictionary<MessageId, IMessage> _messageById = new();
    private readonly HashSet<Peer> _deniedPeers = [];
    private readonly ImmutableHashSet<Peer> _seeds;
    private readonly ImmutableHashSet<Peer> _validators;
    private readonly IMessageHandler[] _handlers = [];
    private readonly PeerService _peerDiscovery;
    private ConcurrentDictionary<Peer, HashSet<MessageId>> _haveDict = new();
    // private PeerCollection? _peers;
    private ServiceCollection? _services;

    public Gossip(
        ITransport transport, ImmutableHashSet<Peer> seeds, ImmutableHashSet<Peer> validators, GossipOptions options)
    {
        _transport = transport;
        _options = options;
        _seeds = seeds;
        _validators = validators;
        _handlers =
        [
            new HaveMessageHandler(_transport, _messageById, _haveDict),
            new WantMessageHandler(_transport, _messageById, _haveDict),
        ];
        _transport.MessageHandlers.AddRange(_handlers);
        _peerDiscovery = new PeerService(_transport);
    }

    public Gossip(ITransport transport)
        : this(transport, [], [], new GossipOptions())
    {
    }

    public MessageValidatorCollection MessageValidators { get; } = [];

    public Peer Peer => _transport.Peer;

    public ImmutableArray<Peer> Peers
    {
        get
        {
            // if (_peers is null)
            // {
            //     throw new InvalidOperationException("Gossip is not running.");
            // }

            return [.. _peerDiscovery.Peers];
        }
    }

    public ImmutableArray<Peer> DeniedPeers => [.. _deniedPeers];

    public void ClearCache()
    {
        _messageById.Clear();
    }

    public void PublishMessage(IMessage message)
    {
        if (_peerDiscovery is null)
        {
            throw new InvalidOperationException("Gossip is not running.");
        }

        ImmutableArray<Peer> peers =
        [
            _transport.Peer,
            .. GetPeersToBroadcast(_peerDiscovery.Peers, DLazy)
        ];

        PublishMessage(peers, message);
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
            _messageById[message.Id] = message;
            _transport.Post(targetPeers, message);
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

    public async Task HeartbeatAsync(CancellationToken cancellationToken)
    {
        var peers = _peerDiscovery?.Peers ?? throw new InvalidOperationException("Gossip is not running.");
        var ids = _messageById.Keys.ToArray();
        if (ids.Length > 0)
        {
            var peersToBroadcast = GetPeersToBroadcast(peers, DLazy);
            var message = new HaveMessage { Ids = [.. ids] };
            _transport.Post(peersToBroadcast, message);
        }

        await SendWantMessageAsync(cancellationToken);
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        await _transport.StartAsync(cancellationToken);
        // _peers = new PeerCollection(_transport.Peer.Address);
        // _peerDiscovery = new PeerService(_transport);
        _peerDiscovery.AddRange(_validators);
        await _peerDiscovery.StartAsync(cancellationToken);
        _services =
        [
            new RefreshTableTask(_peerDiscovery, _options.RefreshTableInterval, _options.RefreshLifespan),
            new RebuildTableTask(_peerDiscovery, _seeds, _options.RebuildTableInterval),
            new HeartbeatTask(this, _options.HeartbeatInterval)
        ];
        if (_seeds.Count > 0)
        {
            try
            {
                await _peerDiscovery.BootstrapAsync(_seeds, 3, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // logging
            }
        }

        await _services.StartAsync(cancellationToken);
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        if (_services is not null)
        {
            await _services.StopAsync(cancellationToken);
            await _services.DisposeAsync();
            _services = null;
        }

        await _peerDiscovery.StopAsync(cancellationToken);
        await _transport.StopAsync(cancellationToken);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _transport.MessageHandlers.RemoveRange(_handlers);
        if (_services is not null)
        {
            await _services.DisposeAsync();
            _services = null;
        }

        await _peerDiscovery.DisposeAsync();
        _messageById.Clear();
        await _transport.DisposeAsync();
        await base.DisposeAsyncCore();
    }

    private ImmutableArray<Peer> GetPeersToBroadcast(IEnumerable<Peer> peers, int count)
    {
        var random = new Random();
        var query = from peer in peers
                    where !_seeds.Contains(peer)
                    orderby random.Next()
                    select peer;

        return [.. query.Take(count)];
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
                var wantMessage = new WantMessage { Ids = [.. idsToGet] };
                try
                {
                    var query = _transport.SendAsync<IMessage>(pair.Key, wantMessage, m => true, cancellationToken);
                    await foreach (var item in query)
                    {
                        _messageById.TryAdd(item.Id, item);
                        // Messagehandlers.HandleAsync(item)
                        // _validateReceivedMessageSubject.OnNext((pair.Key, item));
                        MessageValidators.Validate(item);
                    }
                }
                catch (Exception e)
                {

                }
            });
    }
}
