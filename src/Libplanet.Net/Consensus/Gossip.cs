using Libplanet.Net.Components;
using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net.Consensus;

public sealed class Gossip : IAsyncDisposable
{
    private const int DLazy = 6;
    private readonly ITransport _transport;
    private readonly MessageCollection _messages = new();
    private readonly HashSet<Peer> _deniedPeers = [];
    private readonly IDisposable _handlerRegistration;
    private readonly PeerMessageIdCollection _peerMessageIds = [];
    private readonly MessageRequester _messageRequester;
    private bool _disposed;

    public Gossip(ITransport transport, PeerCollection peers)
    {
        _transport = transport;
        Peers = peers;
        _handlerRegistration = _transport.MessageRouter.RegisterMany(
        [
            new HaveMessageHandler(peers, _messages, _peerMessageIds),
            new WantMessageHandler(_transport, _messages),
        ]);
        _messageRequester = new MessageRequester(_transport, _messages, _peerMessageIds);
    }

    public MessageCollection Messages => _messages;

    public PeerMessageIdCollection PeerMessageIds => _peerMessageIds;

    public Peer Peer => _transport.Peer;

    public PeerCollection Peers { get; }

    public ImmutableArray<Peer> DeniedPeers => [.. _deniedPeers];

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _messageRequester.DisposeAsync();
            _handlerRegistration.Dispose();
            _messages.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public void ClearCache()
    {
        _messages.Clear();
    }

    public void PublishMessage(IMessage message)
    {
        ImmutableArray<Peer> peers =
        [
            _transport.Peer,
            .. GetPeersToBroadcast(Peers, DLazy)
        ];

        PublishMessage(peers, message);
    }

    public void PublishMessage(ImmutableArray<Peer> targetPeers, params IMessage[] messages)
    {
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

    private ImmutableArray<Peer> GetPeersToBroadcast(IEnumerable<Peer> peers, int count)
    {
        var random = new Random();
        var query = from peer in peers
                        // where !_seeds.Contains(peer)
                    orderby random.Next()
                    select peer;

        return [.. query.Take(count)];
    }
}
