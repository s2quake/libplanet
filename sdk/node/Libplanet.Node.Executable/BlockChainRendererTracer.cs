using Libplanet.Node.Services;

namespace Libplanet.Node.API;

internal sealed class BlockChainRendererTracer(
    IBlockChainService blockChainService, ILogger<BlockChainRendererTracer> logger)
    : IHostedService
{
    private readonly ILogger<BlockChainRendererTracer> _logger = logger;
    private IDisposable? _observer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // blockChainService.BlockChain.BlockExecuted.Subscribe(
        //     info => _logger.LogInformation(
        //         "-Pattern2- #{Height} Block end: {Hash}",
        //         info.NewTip.Height,
        //         info.NewTip.BlockHash));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _observer?.Dispose();
        _observer = null;
        return Task.CompletedTask;
    }
}
