using Libplanet.Node.Services;

namespace Libplanet.Node.API;

internal sealed class BlockChainRendererTracer2(
    IRendererService rendererService, ILogger<BlockChainRendererTracer2> logger)
    : IHostedService
{
    private readonly ILogger<BlockChainRendererTracer2> _logger = logger;
    private Observer<RenderBlockInfo>? _observer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _observer = new Observer<RenderBlockInfo>(rendererService.RenderBlockEnd)
        {
            Next = info => _logger.LogInformation(
                "-Pattern2- #{Height} Block end: {Hash}",
                info.NewTip.Index,
                info.NewTip.Hash),
        };
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _observer?.Dispose();
        _observer = null;
        return Task.CompletedTask;
    }
}
