using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net.Components;

public sealed class BlockFetchingResponder(
    Blockchain blockchain, ITransport transport, BlockFetchingResponder.Options options)
    : IDisposable
{
    private readonly IDisposable _handlerRegistration = transport.MessageRouter.RegisterMany(
    [
        new BlockHashRequestMessageHandler(blockchain, transport, options.MaxHashes),
        new BlockRequestMessageHandler(blockchain, transport, options.MaxConcurrentResponses),
    ]);
    private bool _disposed;

    public BlockFetchingResponder(Blockchain blockchain, ITransport transport)
        : this(blockchain, transport, new Options())
    {
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _handlerRegistration.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    public sealed record class Options
    {
        public int MaxHashes { get; init; } = 100;

        public int MaxConcurrentResponses { get; init; } = 3;
    }
}
