using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net.Components;

public sealed class BlockFetchingResponder : IDisposable
{
    private readonly ITransport _transport;
    private readonly IMessageHandler[] _handlers;
    private bool _disposed;

    public BlockFetchingResponder(ITransport transport, Blockchain blockchain, int maxAccessCount)
    {
        _transport = transport;
        _handlers =
        [
            new BlockHashRequestMessageHandler(blockchain, transport),
            new BlockRequestMessageHandler(blockchain, transport, maxAccessCount),
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
