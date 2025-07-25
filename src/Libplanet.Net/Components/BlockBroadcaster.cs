using Libplanet.Net.Messages;

namespace Libplanet.Net.Components;

public sealed class BlockBroadcaster(Blockchain blockchain, PeerDiscovery peerDiscovery) : IDisposable
{
    private readonly IDisposable _subscription = blockchain.TipChanged.Subscribe(e =>
    {
        var message = new BlockSummaryMessage
        {
            GenesisHash = blockchain.Genesis.BlockHash,
            BlockSummary = e.Tip,
        };
        peerDiscovery.Broadcast(message);
    });

    private bool _disposed;

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
