using Libplanet.Net;
using Libplanet.Net.Consensus;
using Libplanet.Node.Options;
using Libplanet.Types;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Services;

internal sealed class ValidatorService : IValidatorService, IHostedService, IAsyncDisposable
{
    private readonly Transport _transport;
    private readonly ConsensusService _consensusService;

    public ValidatorService(
        IOptions<NodeOptions> nodeOptions,
        IOptions<ValidatorOptions> validatorOptions,
        BlockchainService blockchainService,
        ILoggerFactory loggerFactory)
    {
        var privateKey = PrivateKey.Parse(nodeOptions.Value.PrivateKey);
        var signer = privateKey.AsSigner();
        var consensusServiceOptions = new ConsensusServiceOptions
        {
            BlockInterval = TimeSpan.FromSeconds(4),
            ConsensusOptions = new ConsensusOptions
            {
                Logger = loggerFactory.CreateLogger<Consensus>(),
            },
            Logger = loggerFactory.CreateLogger<ConsensusService>(),
        };
        var transportOptions = new TransportOptions
        {
            Port = validatorOptions.Value.Port,
            Logger = loggerFactory.CreateLogger<Transport>(),
        };
        _transport = new Transport(signer, transportOptions);
        _consensusService = new ConsensusService(signer,
            blockchainService.Blockchain, _transport, consensusServiceOptions);
    }

    public Address Address => _consensusService.Address;

    public async ValueTask DisposeAsync()
    {
        await _consensusService.DisposeAsync();
        await _transport.DisposeAsync();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _transport.StartAsync(cancellationToken);
        await _consensusService.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _consensusService.StopAsync(cancellationToken);
        await _transport.StopAsync(cancellationToken);
    }
}
