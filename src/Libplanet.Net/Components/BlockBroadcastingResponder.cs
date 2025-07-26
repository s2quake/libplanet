using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net.Components;

public sealed class BlockBroadcastingResponder : IDisposable
{
    private readonly ITransport _transport;
    private readonly IMessageHandler[] _handlers;
    private bool _disposed;

    public BlockBroadcastingResponder(ITransport transport, Blockchain blockchain, BlockDemandCollection blockDemands)
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
