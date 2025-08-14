using Libplanet.Net.Components;
using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net.Consensus;

public sealed class Gossip : IAsyncDisposable
{
    private const int DLazy = 6;
    private readonly ITransport _transport;
    private readonly MessageCollection _messages = new();
    private readonly IDisposable _handlerRegistration;
    private readonly PeerMessageIdCollection _peerMessageIds = [];
    private readonly MessageRequester _messageRequester;
    private bool _disposed;

    public Gossip(ITransport transport, PeerCollection peers)
    {
        _transport = transport;
        Peers = peers;
        DeniedPeers = new PeerCollection(transport.Peer.Address);
        _handlerRegistration = _transport.MessageRouter.RegisterMany(
        [
            new HaveMessageHandler(peers, _messages, _peerMessageIds),
            new WantMessageHandler(_transport, _messages),
        ]);
        _messageRequester = new MessageRequester(_transport, _messages, _peerMessageIds);
    }

    public IMessageRouter MessageRouter => _transport.MessageRouter;

    public MessageCollection Messages => _messages;

    public PeerMessageIdCollection PeerMessageIds => _peerMessageIds;

    public Peer Peer => _transport.Peer;

    public PeerCollection Peers { get; }

    public PeerCollection DeniedPeers { get; }

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

    public void Broadcast(IMessage message)
    {
        ImmutableArray<Peer> peers =
        [
            _transport.Peer,
            .. GetPeersToBroadcast(Peers, DLazy)
        ];

        Broadcast(peers, message);
    }

    public void Broadcast(ImmutableArray<Peer> targetPeers, params IMessage[] messages)
    {
        foreach (var message in messages)
        {
            _messages.TryAdd(message);
            _transport.Post(targetPeers, message);
        }
    }

    private static ImmutableArray<Peer> GetPeersToBroadcast(IEnumerable<Peer> peers, int count)
    {
        var random = new Random();
        var query = from peer in peers
                    orderby random.Next()
                    select peer;

        return [.. query.Take(count)];
    }
}
