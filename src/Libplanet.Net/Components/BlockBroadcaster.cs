using System.Reactive.Subjects;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net.Components;

public sealed class BlockBroadcaster : IDisposable
{
    private readonly Subject<(ImmutableArray<Peer>, Block)> _broadcastedSubject = new();

    private readonly IDisposable _subscription;
    private bool _disposed;

    public IObservable<(ImmutableArray<Peer>, Block)> Broadcasted => _broadcastedSubject;

    public BlockBroadcaster(Blockchain blockchain, PeerExplorer peerDiscovery)
    {
        _subscription = blockchain.TipChanged.Subscribe(e =>
        {
            var tip = e;
            var message = new BlockSummaryMessage
            {
                GenesisBlockHash = blockchain.Genesis.BlockHash,
                BlockSummary = tip,
            };
            var peers = peerDiscovery.Broadcast(message);
            _broadcastedSubject.OnNext((peers, tip));
        });
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _subscription.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
