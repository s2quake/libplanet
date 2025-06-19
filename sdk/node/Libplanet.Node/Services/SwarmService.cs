using System.Net;
using Libplanet.Net;
using Libplanet.Net.Consensus;
using Libplanet.Net.Transports;
using Libplanet.Node.Options;
using Libplanet.Types;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using R3;

namespace Libplanet.Node.Services;

internal sealed class SwarmService(
    IBlockChainService blockChainService,
    IOptions<SwarmOptions> options,
    IOptions<ValidatorOptions> validatorOptions,
    ILogger<SwarmService> logger)
    : IHostedService, ISwarmService, IAsyncDisposable
{
    private readonly SwarmOptions _options = options.Value;
    private readonly ValidatorOptions _validatorOptions = validatorOptions.Value;
    private readonly ILogger<SwarmService> _logger = logger;
    private readonly Subject<Unit> _started = new();
    private readonly Subject<Unit> _stopped = new();

    private IObservable<Unit>? _startedObservable;
    private IObservable<Unit>? _stoppedObservable;

    private Swarm? _swarm;
    private Task _startTask = Task.CompletedTask;
    private Seed? _blocksyncSeed;
    private Seed? _consensusSeed;

    IObservable<Unit> ISwarmService.Started
        => _startedObservable ??= _started.AsSystemObservable();

    IObservable<Unit> ISwarmService.Stopped
        => _stoppedObservable ??= _stopped.AsSystemObservable();

    public bool IsRunning => _swarm is not null;

    public Swarm Swarm => _swarm ?? throw new InvalidOperationException("Node is not running.");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_swarm is not null)
        {
            throw new InvalidOperationException("Node is already running.");
        }

        var seedPrivateKey = new PrivateKey();
        var blockChain = blockChainService.BlockChain;

        if (_options.BlocksyncSeedPeer == string.Empty)
        {
            _blocksyncSeed = new Seed(new()
            {
                PrivateKey = ByteUtility.Hex(seedPrivateKey.Bytes),
                EndPoint = EndPointUtility.ToString(EndPointUtility.Next()),
                AppProtocolVersion = _options.AppProtocolVersion,
            });
            _options.BlocksyncSeedPeer = _blocksyncSeed.BoundPeer.ToString();
            await _blocksyncSeed.StartAsync(cancellationToken);
        }

        if (_validatorOptions.ConsensusSeedPeer == string.Empty)
        {
            _consensusSeed = new Seed(new()
            {
                PrivateKey = ByteUtility.Hex(seedPrivateKey.Bytes),
                EndPoint = EndPointUtility.ToString(EndPointUtility.Next()),
                AppProtocolVersion = _options.AppProtocolVersion,
            });
            _validatorOptions.ConsensusSeedPeer = _consensusSeed.BoundPeer.ToString();
            await _consensusSeed.StartAsync(cancellationToken);
        }

        var nodeOptions = _options;
        var privateKey = PrivateKey.Parse(nodeOptions.PrivateKey);
        var appProtocolVersion = Protocol.FromToken(nodeOptions.AppProtocolVersion);
        var trustedAppProtocolVersionSigners = nodeOptions.TrustedAppProtocolVersionSigners
            .Select(Address.Parse).ToArray();
        var swarmEndPoint = (DnsEndPoint)EndPointUtility.Parse(nodeOptions.EndPoint);
        var swarmTransport = CreateTransport(
            privateKey: privateKey,
            endPoint: swarmEndPoint,
            protocol: appProtocolVersion);
        var blocksyncSeedPeer = Net.Peer.Parse(nodeOptions.BlocksyncSeedPeer);
        var swarmOptions = new Net.Options.SwarmOptions
        {
            StaticPeers = [blocksyncSeedPeer],
            BootstrapOptions = new()
            {
                SeedPeers = [blocksyncSeedPeer],
            },
        };

        var consensusTransport = _validatorOptions.IsEnabled
            ? CreateConsensusTransport(privateKey, appProtocolVersion, _validatorOptions)
            : null;
        var consensusReactorOption = _validatorOptions.IsEnabled
            ? CreateConsensusReactorOption(privateKey, _validatorOptions)
            : (ConsensusReactorOptions?)null;

        _swarm = new Swarm(
            blockChain: blockChain,
            privateKey: privateKey,
            transport: swarmTransport,
            options: swarmOptions,
            consensusTransport: consensusTransport,
            consensusOption: consensusReactorOption);
        _startTask = _swarm.StartAsync(cancellationToken: default);
        _logger.LogDebug("Node.Swarm is starting: {Address}", _swarm.Address);
        await _swarm.BootstrapAsync(cancellationToken: default);
        _logger.LogDebug("Node.Swarm is bootstrapped: {Address}", _swarm.Address);
        _started.OnNext(Unit.Default);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_swarm is null)
        {
            throw new InvalidOperationException("Node is not running.");
        }

        await _swarm.StopAsync(cancellationToken: cancellationToken);
        await _startTask;
        _logger.LogDebug("Node.Swarm is stopping: {Address}", _swarm.Address);
        await _swarm.DisposeAsync();
        _logger.LogDebug("Node.Swarm is stopped: {Address}", _swarm.Address);

        _swarm = null;
        _startTask = Task.CompletedTask;

        if (_consensusSeed is not null)
        {
            await _consensusSeed.StopAsync(cancellationToken: default);
            _consensusSeed = null;
        }

        if (_blocksyncSeed is not null)
        {
            await _blocksyncSeed.StopAsync(cancellationToken: default);
            _blocksyncSeed = null;
        }

        _stopped.OnNext(Unit.Default);
    }

    public async ValueTask DisposeAsync()
    {
        if (_swarm is not null)
        {
            await _swarm.StopAsync(cancellationToken: default);
            await _swarm.DisposeAsync();
        }

        await (_startTask ?? Task.CompletedTask);
        _startTask = Task.CompletedTask;

        if (_consensusSeed is not null)
        {
            await _consensusSeed.StopAsync(cancellationToken: default);
            _consensusSeed = null;
        }

        if (_blocksyncSeed is not null)
        {
            await _blocksyncSeed.StopAsync(cancellationToken: default);
            _blocksyncSeed = null;
        }
    }

    private static NetMQTransport CreateTransport(PrivateKey privateKey, DnsEndPoint endPoint, Protocol protocol)
    {
        var transportOptions = new Net.Options.TransportOptions
        {
            Protocol = protocol,
            Host = endPoint.Host,
            Port = endPoint.Port,
        };
        return new(privateKey, transportOptions);
    }

    private static ConsensusReactorOptions CreateConsensusReactorOption(
        PrivateKey privateKey, ValidatorOptions options)
    {
        var consensusSeedPeer = Net.Peer.Parse(options.ConsensusSeedPeer);
        var consensusEndPoint = (DnsEndPoint)EndPointUtility.Parse(options.EndPoint);
        return new ConsensusReactorOptions
        {
            SeedPeers = [consensusSeedPeer],
            Port = consensusEndPoint.Port,
            PrivateKey = privateKey,
            TargetBlockInterval = TimeSpan.FromSeconds(2),
            ContextOptions = new(),
        };
    }

    private static NetMQTransport CreateConsensusTransport(
        PrivateKey privateKey, Protocol protocol, ValidatorOptions options)
    {
        var consensusEndPoint = (DnsEndPoint)EndPointUtility.Parse(options.EndPoint);
        return CreateTransport(privateKey, consensusEndPoint, protocol);
    }
}
