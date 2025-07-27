using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net.Components;

public sealed class BlockFetchingHandler : IDisposable
{
    private readonly ITransport _transport;
    private readonly IMessageHandler[] _handlers;
    private bool _disposed;

    public BlockFetchingHandler(Blockchain blockchain, ITransport transport)
        : this(blockchain, transport, 3)
    {
    }

    public BlockFetchingHandler(Blockchain blockchain, ITransport transport, int maxAccessCount)
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
