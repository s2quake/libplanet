using Libplanet.Node.Services;

namespace Libplanet.Node.API;

internal sealed class BlockChainRendererTracer1(
    IRendererService rendererService, ILogger<BlockChainRendererTracer1> logger)
    : IObserver<RenderBlockInfo>, IHostedService
{
    private readonly ILogger<BlockChainRendererTracer1> _logger = logger;
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = rendererService.RenderBlockEnd.Subscribe(this);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        _subscription = null;
        return Task.CompletedTask;
    }

    void IObserver<RenderBlockInfo>.OnCompleted()
    {
    }

    void IObserver<RenderBlockInfo>.OnError(Exception error)
    {
    }

    void IObserver<RenderBlockInfo>.OnNext(RenderBlockInfo value)
    {
        _logger.LogInformation(
            "[Pattern] #{Height} Block end: {Hash}",
            value.NewTip.Index,
            value.NewTip.Hash);
    }
}
