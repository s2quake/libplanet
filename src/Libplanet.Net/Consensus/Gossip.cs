using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Components;
using Libplanet.Net.Consensus.GossipMessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Net.Services;
using Libplanet.Net.Tasks;

namespace Libplanet.Net.Consensus;

public sealed class Gossip : ServiceBase
{
    private const int DLazy = 6;
    private readonly ITransport _transport;
    private readonly GossipOptions _options;
    private readonly MessageCollection _messages = new();
    private readonly HashSet<Peer> _deniedPeers = [];
    private readonly ImmutableHashSet<Peer> _seeds;
    private readonly IDisposable _handlerRegistration;
    private readonly PeerExplorer _peerExplorer;
    private readonly ServiceCollection _services;
    private PeerMessageIdCollection _haveDict = [];

    public Gossip(
        ITransport transport, ImmutableHashSet<Peer> seeds, ImmutableHashSet<Peer> validators, GossipOptions options)
    {
        _transport = transport;
        _options = options;
        _seeds = seeds;
        _handlerRegistration = _transport.MessageRouter.RegisterMany(
        [
            new HaveMessageHandler(_transport, _messages, _haveDict),
            new WantMessageHandler(_transport, _messages, _haveDict),
        ]);
        _peerExplorer = new PeerExplorer(_transport, new PeerExplorerOptions
        {
            SeedPeers = seeds,
            KnownPeers = validators,
        });
        _services =
        [
            new HeartbeatTask(this, _options.HeartbeatInterval)
        ];
    }

    public Gossip(ITransport transport)
        : this(transport, [], [], new GossipOptions())
    {
    }

    public MessageValidatorCollection MessageValidators { get; } = [];

    public Peer Peer => _transport.Peer;

    public ImmutableArray<Peer> Peers => [.. _peerExplorer.Peers];

    public ImmutableArray<Peer> DeniedPeers => [.. _deniedPeers];

    public void ClearCache()
    {
        _messages.Clear();
    }

    public void PublishMessage(IMessage message)
    {
        if (_peerExplorer is null)
        {
            throw new InvalidOperationException("Gossip is not running.");
        }

        ImmutableArray<Peer> peers =
        [
            _transport.Peer,
            .. GetPeersToBroadcast(_peerExplorer.Peers, DLazy)
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
            _messages.TryAdd(message);
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
        var peers = _peerExplorer?.Peers ?? throw new InvalidOperationException("Gossip is not running.");
        var ids = _messages.Select(item => item.Id).ToArray();
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
        await _services.StartAsync(cancellationToken);
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        await _services.StopAsync(cancellationToken);
        await _transport.StopAsync(cancellationToken);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _handlerRegistration.Dispose();
        await _services.DisposeAsync();
        _peerExplorer.Dispose();
        _messages.Clear();
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
        _haveDict = [];
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
                        _messages.TryAdd(item);
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
