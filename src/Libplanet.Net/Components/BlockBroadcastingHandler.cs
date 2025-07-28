using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net.Components;

public sealed class BlockBroadcastingHandler : IDisposable
{
    private readonly ITransport _transport;
    private readonly IMessageHandler[] _handlers;
    private bool _disposed;

    public BlockBroadcastingHandler(Blockchain blockchain, ITransport transport, BlockDemandCollection blockDemands)
    {
        _transport = transport;
        _handlers =
        [
            new BlockSummaryMessageHandler(blockchain, blockDemands),
        ];
        _transport.MessageHandlers.AddRange(_handlers);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _transport.MessageHandlers.RemoveRange(_handlers);
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
