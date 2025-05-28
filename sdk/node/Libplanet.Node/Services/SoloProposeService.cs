using Libplanet.Node.Options;
using Libplanet.Types;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Services;

internal sealed class SoloProposeService : BackgroundService
{
    private readonly Blockchain _blockChain;
    private readonly PrivateKey _privateKey;
    private readonly TimeSpan _blockInterval;
    private readonly ILogger<SoloProposeService> _logger;

    public SoloProposeService(
        IBlockChainService blockChainService,
        ILogger<SoloProposeService> logger,
        IOptions<SoloOptions> soloProposeOption)
    {
        _blockChain = blockChainService.BlockChain;
        var options = soloProposeOption.Value;
        _privateKey = options.PrivateKey is null
            ? new PrivateKey()
            : PrivateKey.Parse(options.PrivateKey);
        _blockInterval = TimeSpan.FromMilliseconds(options.BlockInterval);
        _logger = logger;
        _logger.LogInformation(
            "SoloProposeService initialized. Interval: {BlockInterval}",
            _blockInterval);
    }

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ProposeBlockAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Timed Hosted Service is stopping.");
        }
    }

    private async Task ProposeBlockAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ProposeBlock();
            await Task.Delay(_blockInterval, cancellationToken);
        }
    }

    private void ProposeBlock()
    {
        var block = _blockChain.ProposeBlock(_privateKey);
        var blockCommit = new BlockCommit
        {
            BlockHash = block.BlockHash,
            Votes =
            [
                new VoteMetadata
                {
                    Validator = _privateKey.Address,
                    Flag = VoteFlag.PreCommit,
                }.Sign(_privateKey)
            ],
        };
        _blockChain.Append(block, blockCommit);

        _logger.LogInformation(
            "Proposed block: {Height}: {Hash}",
            block.Height,
            block.BlockHash);
    }
}
