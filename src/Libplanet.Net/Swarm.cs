using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.State;
#if NETSTANDARD2_0
using Libplanet.Types;
#endif
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Protocols;
using Libplanet.Net.Transports;
using Libplanet.Serialization;
using Libplanet.Types;
using Nito.AsyncEx;
using Serilog;
using System.ServiceModel;
using Org.BouncyCastle.Tls;

namespace Libplanet.Net;

public partial class Swarm : IAsyncDisposable
{

    private readonly PrivateKey _privateKey;

    private readonly AsyncLock _runningMutex;

    private readonly ILogger _logger;
    private readonly ConsensusReactor _consensusReactor;
    private readonly TxFetcher _txFetcher;
    private readonly IDisposable _txFetcherSubscription;
    private readonly EvidenceFetcher _evidenceFetcher;
    private readonly IDisposable _evidenceFetcherSubscription;

    private CancellationTokenSource _workerCancellationTokenSource;
    private CancellationToken _cancellationToken;
    private IDisposable? _tipChangedSubscription;



    private bool _disposed;

    /// <summary>
    /// Creates a <see cref="Swarm"/>.  This constructor in only itself does not start
    /// any communication with the network.
    /// </summary>
    /// <param name="blockChain">A blockchain to publicize on the network.</param>
    /// <param name="privateKey">A private key to sign messages.  The public part of
    /// this key become a part of its end address for being pointed by peers.</param>
    /// <param name="transport">The <see cref="ITransport"/> to use for
    /// network communication in block synchronization.</param>
    /// <param name="options">Options for <see cref="Swarm"/>.</param>
    /// <param name="consensusTransport">The <see cref="ITransport"/> to use for
    /// network communication in consensus.
    /// If null is given, the node cannot join block consensus.
    /// </param>
    /// <param name="consensusOption"><see cref="ConsensusReactorOptions"/> for
    /// initialize <see cref="ConsensusReactor"/>.</param>
    public Swarm(
        Blockchain blockChain,
        PrivateKey privateKey,
        ITransport transport,
        SwarmOptions options = null,
        ITransport consensusTransport = null,
        ConsensusReactorOptions? consensusOption = null)
    {
        Blockchain = blockChain ?? throw new ArgumentNullException(nameof(blockChain));
        _privateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
        LastSeenTimestamps =
            new ConcurrentDictionary<Peer, DateTimeOffset>();
        BlockHeaderReceived = new AsyncAutoResetEvent();
        BlockAppended = new AsyncAutoResetEvent();
        BlockReceived = new AsyncAutoResetEvent();

        _runningMutex = new AsyncLock();

        string loggerId = _privateKey.Address.ToString("raw", null);
        _logger = Log
            .ForContext<Swarm>()
            .ForContext("Source", nameof(Swarm))
            .ForContext("SwarmId", loggerId);

        Options = options ?? new SwarmOptions();
        TxCompletion = new TxCompletion(Blockchain, GetTxsAsync, BroadcastTxs);
        EvidenceCompletion =
            new EvidenceCompletion<Peer>(
                Blockchain, GetEvidenceAsync, BroadcastEvidence);
        RoutingTable = new RoutingTable(Address, Options.TableSize, Options.BucketSize);

        // FIXME: after the initialization of NetMQTransport is fully converted to asynchronous
        // code, the portion initializing the swarm in Agent.cs in NineChronicles should be
        // fixed. for context, refer to
        // https://github.com/planetarium/libplanet/discussions/2303.
        Transport = transport;
        _txFetcher = new TxFetcher(Blockchain, Transport, Options.TimeoutOptions);
        _evidenceFetcher = new EvidenceFetcher(Blockchain, Transport, Options.TimeoutOptions);
        _processBlockDemandSessions = new ConcurrentDictionary<Peer, int>();
        Transport.MessageReceived.Subscribe(ProcessMessageHandler);
        PeerDiscovery = new Kademlia(RoutingTable, Transport, Address);
        BlockDemandTable = new BlockDemandDictionary(Options.BlockDemandLifespan);
        BlockCandidateTable = new BlockCandidateTable();
        _txFetcherSubscription = _txFetcher.Received.Subscribe(e => BroadcastTxs(e.Peer, e.Items));
        _evidenceFetcherSubscription = _evidenceFetcher.Received.Subscribe(e => BroadcastEvidence(e.Peer, e.Items));

        // Regulate heavy tasks. Treat negative value as 0.
        var taskRegulationOptions = Options.TaskRegulationOptions;
        _transferBlocksSemaphore =
            new NullableSemaphore(taskRegulationOptions.MaxTransferBlocksTaskCount);
        _transferTxsSemaphore =
            new NullableSemaphore(taskRegulationOptions.MaxTransferTxsTaskCount);
        _transferEvidenceSemaphore =
            new NullableSemaphore(taskRegulationOptions.MaxTransferTxsTaskCount);

        // Initialize consensus reactor.
        if (consensusTransport is { } && consensusOption is { } consensusReactorOption)
        {
            _consensusReactor = new ConsensusReactor(
                consensusTransport, Blockchain, consensusReactorOption);
        }
    }

    ~Swarm()
    {
        // FIXME If possible, we should stop Swarm appropriately here.
        if (Running)
        {
            _logger.Warning(
                "Swarm is scheduled to destruct, but Transport progress is still running");
        }
    }

    public bool Running => Transport?.IsRunning ?? false;

    public bool ConsensusRunning => _consensusReactor?.IsRunning ?? false;

    public DnsEndPoint EndPoint => AsPeer is Peer boundPeer ? boundPeer.EndPoint : null;

    public Address Address => _privateKey.Address;

    public Peer AsPeer => Transport?.Peer;

    public IDictionary<Peer, DateTimeOffset> LastSeenTimestamps { get; private set; }

    public IReadOnlyList<Peer> Peers => RoutingTable.Peers;

    public IReadOnlyList<Peer> Validators => _consensusReactor?.Validators;

    public Blockchain Blockchain { get; private set; }

    public Protocol AppProtocolVersion => Transport.Protocol;

    internal RoutingTable RoutingTable { get; }

    internal Kademlia PeerDiscovery { get; }

    internal ITransport Transport { get; }

    internal TxCompletion TxCompletion { get; }

    internal EvidenceCompletion<Peer> EvidenceCompletion { get; }

    internal AsyncAutoResetEvent TxReceived => TxCompletion?.TxReceived;

    internal AsyncAutoResetEvent EvidenceReceived => EvidenceCompletion?.EvidenceReceived;

    internal AsyncAutoResetEvent BlockHeaderReceived { get; }

    internal AsyncAutoResetEvent BlockReceived { get; }

    // FIXME: Should have a unit test.
    internal AsyncAutoResetEvent BlockAppended { get; }

    // FIXME: We need some sort of configuration method for it.
    internal int FindNextHashesChunkSize { get; set; } = 500;

    internal AsyncAutoResetEvent BlockDownloadStarted { get; } = new AsyncAutoResetEvent();

    internal SwarmOptions Options { get; }

    // FIXME: This should be exposed in a better way.
    internal ConsensusReactor ConsensusReactor => _consensusReactor;


    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_workerCancellationTokenSource is not null)
            {
                await _workerCancellationTokenSource.CancelAsync();
            }

            TxCompletion?.Dispose();
            await Transport.DisposeAsync();
            await _consensusReactor.DisposeAsync();
            _workerCancellationTokenSource?.Dispose();
            _workerCancellationTokenSource = null;
            _disposed = true;
        }
    }

    public async Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        await StopAsync(TimeSpan.FromSeconds(1), cancellationToken);
    }

    public async Task StopAsync(
        TimeSpan waitFor,
        CancellationToken cancellationToken = default)
    {
        _logger.Debug("Stopping watching " + nameof(Blockchain) + " for tip changes...");
        _tipChangedSubscription?.Dispose();
        _tipChangedSubscription = null;

        _logger.Debug($"Stopping {nameof(Swarm)}...");
        using (await _runningMutex.LockAsync())
        {
            await Transport.StopAsync(cancellationToken);
            if (_consensusReactor is { })
            {
                await _consensusReactor.StopAsync(cancellationToken);
            }
        }

        BlockDemandTable = new BlockDemandDictionary(Options.BlockDemandLifespan);
        BlockCandidateTable = new BlockCandidateTable();
        _logger.Debug($"{nameof(Swarm)} stopped");
    }

    /// <summary>
    /// Starts to periodically synchronize the <see cref="Blockchain"/>.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token used to propagate notification that this
    /// operation should be canceled.
    /// </param>
    /// <returns>An awaitable task without value.</returns>
    /// <exception cref="SwarmException">Thrown when this <see cref="Swarm"/> instance is
    /// already <see cref="Running"/>.</exception>
    /// <remarks>If the <see cref="Blockchain"/> has no blocks at all or there are long behind
    /// blocks to caught in the network this method could lead to unexpected behaviors, because
    /// this tries to render <em>all</em> actions in the behind blocks so that there are
    /// a lot of calls to methods of <see cref="Blockchain.Renderers"/> in a short
    /// period of time.  This can lead a game startup slow.  If you want to omit rendering of
    /// these actions in the behind blocks use
    /// <see cref="PreloadAsync(IProgress{BlockSyncState}, CancellationToken)"/>
    /// method too.</remarks>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await StartAsync(
            Options.TimeoutOptions.DialTimeout,
            Options.BlockBroadcastInterval,
            Options.TxBroadcastInterval,
            cancellationToken);
    }

    /// <summary>
    /// Starts to periodically synchronize the <see cref="Blockchain"/>.
    /// </summary>
    /// <param name="dialTimeout">
    /// When the <see cref="Swarm"/> tries to dial each peer in <see cref="Peers"/>,
    /// the dial-up is cancelled after this timeout, and it tries another peer.
    /// If <see langword="null"/> is given it never gives up dial-ups.
    /// </param>
    /// <param name="broadcastBlockInterval">Time interval between each broadcast of
    /// chain tip.</param>
    /// <param name="broadcastTxInterval">Time interval between each broadcast of staged
    /// transactions.</param>
    /// <param name="cancellationToken">
    /// A cancellation token used to propagate notification that this
    /// operation should be canceled.
    /// </param>
    /// <returns>An awaitable task without value.</returns>
    /// <exception cref="SwarmException">Thrown when this <see cref="Swarm"/> instance is
    /// already <see cref="Running"/>.</exception>
    /// <remarks>If the <see cref="Blockchain"/> has no blocks at all or there are long behind
    /// blocks to caught in the network this method could lead to unexpected behaviors, because
    /// this tries to render <em>all</em> actions in the behind blocks so that there are
    /// a lot of calls to methods of <see cref="Blockchain.Renderers"/> in a short
    /// period of time.  This can lead a game startup slow.  If you want to omit rendering of
    /// these actions in the behind blocks use
    /// <see cref="PreloadAsync(IProgress{BlockSyncState}, CancellationToken)"/>
    /// method too.</remarks>
    public async Task StartAsync(
        TimeSpan dialTimeout,
        TimeSpan broadcastBlockInterval,
        TimeSpan broadcastTxInterval,
        CancellationToken cancellationToken = default)
    {
        Task<Task> runner;
        using (await _runningMutex.LockAsync().ConfigureAwait(false))
        {
            _workerCancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
                _workerCancellationTokenSource.Token, cancellationToken)
            .Token;

            if (Transport.IsRunning)
            {
                throw new SwarmException("Swarm is already running.");
            }

            _logger.Debug("Starting swarm...");
            _logger.Debug("Peer information : {Peer}", AsPeer);

            _logger.Debug("Watching the " + nameof(Blockchain) + " for tip changes...");
            _tipChangedSubscription = Blockchain.TipChanged.Subscribe(OnBlockChainTipChanged);

            var tasks = new List<Func<Task>>
            {
                () => Transport.StartAsync(_cancellationToken),
                () => BroadcastBlockAsync(broadcastBlockInterval, _cancellationToken),
                () => BroadcastTxAsync(broadcastTxInterval, _cancellationToken),
                () => FillBlocksAsync(_cancellationToken),
                () => PollBlocksAsync(
                    dialTimeout,
                    Options.TipLifespan,
                    Options.MaximumPollPeers,
                    _cancellationToken),
                () => ConsumeBlockCandidates(
                    TimeSpan.FromMilliseconds(10), _cancellationToken),
                () => RefreshTableAsync(
                    Options.RefreshPeriod,
                    Options.RefreshLifespan,
                    _cancellationToken),
                () => RebuildConnectionAsync(TimeSpan.FromMinutes(30), _cancellationToken),
                () => BroadcastEvidenceAsync(broadcastTxInterval, _cancellationToken),
            };

            if (_consensusReactor is { })
            {
                tasks.Add(() => _consensusReactor.StartAsync(_cancellationToken));
            }

            if (Options.StaticPeers.Any())
            {
                tasks.Add(
                    () => MaintainStaticPeerAsync(
                        Options.StaticPeersMaintainPeriod,
                        _cancellationToken));
            }

            runner = Task.WhenAny(tasks.Select(CreateLongRunningTask));
        }

        try
        {
            _logger.Debug("Swarm started");
            await await runner;
        }
        catch (OperationCanceledException e)
        {
            _logger.Warning(e, "{MethodName}() is canceled", nameof(StartAsync));
            throw;
        }
        catch (Exception e)
        {
            _logger.Error(
                e,
                "An unexpected exception occurred during {MethodName}()",
                nameof(StartAsync));
            throw;
        }
    }

    /// <summary>
    /// Join to the peer-to-peer network using seed peers.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token used to propagate notification
    /// that this operation should be canceled.</param>
    /// <returns>An awaitable task without value.</returns>
    /// <exception cref="SwarmException">Thrown when this <see cref="Swarm"/> instance is
    /// not <see cref="Running"/>.</exception>
    public async Task BootstrapAsync(CancellationToken cancellationToken = default)
    {
        await BootstrapAsync(
            seedPeers: Options.BootstrapOptions.SeedPeers,
            dialTimeout: Options.BootstrapOptions.DialTimeout,
            searchDepth: Options.BootstrapOptions.SearchDepth,
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Join to the peer-to-peer network using seed peers.
    /// </summary>
    /// <param name="seedPeers">List of seed peers.</param>
    /// <param name="dialTimeout">Timeout for connecting to peers.</param>
    /// <param name="searchDepth">Maximum recursion depth when finding neighbors of
    /// current <see cref="Peer"/> from seed peers.</param>
    /// <param name="cancellationToken">A cancellation token used to propagate notification
    /// that this operation should be canceled.</param>
    /// <returns>An awaitable task without value.</returns>
    /// <exception cref="SwarmException">Thrown when this <see cref="Swarm"/> instance is
    /// not <see cref="Running"/>.</exception>
    public async Task BootstrapAsync(
        IEnumerable<Peer> seedPeers,
        TimeSpan? dialTimeout,
        int searchDepth,
        CancellationToken cancellationToken = default)
    {
        if (seedPeers is null)
        {
            throw new ArgumentNullException(nameof(seedPeers));
        }

        IReadOnlyList<Peer> peersBeforeBootstrap = RoutingTable.Peers;

        await PeerDiscovery.BootstrapAsync(
            seedPeers,
            searchDepth,
            cancellationToken).ConfigureAwait(false);

        if (!Transport.IsRunning)
        {
            // Mark added peers as stale if bootstrap is called before transport is running
            // FIXME: Peers added before bootstrap might be updated.
            foreach (Peer peer in RoutingTable.Peers.Except(peersBeforeBootstrap))
            {
                RoutingTable.AddPeer(peer, DateTimeOffset.MinValue);
            }
        }
    }

    /// <summary>
    /// Broadcasts the given block to peers.
    /// <para>The message is immediately broadcasted, and it is done if the same block has
    /// already been broadcasted before.</para>
    /// </summary>
    /// <param name="block">The block to broadcast to peers.</param>
    /// <remarks>It does not have to be called manually, because <see cref="Swarm"/> in
    /// itself watches <see cref="Blockchain"/> for <see cref="Blockchain.Tip"/> changes and
    /// immediately broadcasts updates if anything changes.</remarks>
    public void BroadcastBlock(Block block)
    {
        BroadcastBlock(default, block);
    }

    public void BroadcastTxs(IEnumerable<Transaction> txs)
    {
        BroadcastTxs(null, txs);
    }

    /// <summary>
    /// Gets the <see cref="PeerChainState"/> of the connected <see cref="Peers"/>.
    /// </summary>
    /// <param name="dialTimeout">
    /// When the <see cref="Swarm"/> tries to dial each peer in <see cref="Peers"/>,
    /// the dial-up is cancelled after this timeout, and it tries another peer.
    /// If <see langword="null"/> is given it never gives up dial-ups.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token used to propagate notification that this
    /// operation should be canceled.
    /// </param>
    /// <returns><see cref="PeerChainState"/> of the connected <see cref="Peers"/>.</returns>
    public async Task<IEnumerable<PeerChainState>> GetPeerChainStateAsync(
        TimeSpan? dialTimeout,
        CancellationToken cancellationToken)
    {
        // FIXME: It would be better if it returns IAsyncEnumerable<PeerChainState> instead.
        return (await DialExistingPeers(dialTimeout, int.MaxValue, cancellationToken))
            .Select(pp =>
                new PeerChainState(
                    pp.Item1,
                    pp.Item2?.TipIndex ?? -1));
    }

    /// <summary>
    /// Preemptively downloads blocks from registered <see cref="Peer"/>s.
    /// </summary>
    /// <param name="progress">
    /// An instance that receives progress updates for block downloads.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token used to propagate notification that this
    /// operation should be canceled.
    /// </param>
    /// <returns>
    /// A task without value.
    /// You only can <c>await</c> until the method is completed.
    /// </returns>
    /// <remarks>This does not render downloaded <see cref="IAction"/>s, but fills states only.
    /// </remarks>
    /// <exception cref="AggregateException">Thrown when the given the block downloading is
    /// failed.</exception>
    public async Task PreloadAsync(
        IProgress<double> progress = null,
        CancellationToken cancellationToken = default)
    {
        await PreloadAsync(
            Options.PreloadOptions.DialTimeout,
            Options.PreloadOptions.TipDeltaThreshold,
            progress,
            cancellationToken);
    }

    /// <summary>
    /// Preemptively downloads blocks from registered <see cref="Peer"/>s.
    /// </summary>
    /// <param name="dialTimeout">
    /// When the <see cref="Swarm"/> tries to dial each peer in <see cref="Peers"/>,
    /// the dial-up is cancelled after this timeout, and it tries another peer.
    /// If <see langword="null"/> is given it never gives up dial-ups.
    /// </param>
    /// <param name="tipDeltaThreshold">The threshold of the difference between the topmost tip
    /// among peers and the local tip.  If the local tip is still behind the topmost tip among
    /// peers by more than this threshold after a preloading is once done, the preloading
    /// is repeated.</param>
    /// <param name="progress">
    /// An instance that receives progress updates for block downloads.
    /// </param>
    /// <param name="cancellationToken">
    /// A cancellation token used to propagate notification that this
    /// operation should be canceled.
    /// </param>
    /// <returns>
    /// A task without value.
    /// You only can <c>await</c> until the method is completed.
    /// </returns>
    /// <remarks>This does not render downloaded <see cref="IAction"/>s, but fills states only.
    /// </remarks>
    /// <exception cref="AggregateException">Thrown when the given the block downloading is
    /// failed.</exception>
    public async Task PreloadAsync(
        TimeSpan? dialTimeout,
        long tipDeltaThreshold,
        IProgress<double> progress = null,
        CancellationToken cancellationToken = default)
    {
        using CancellationTokenRegistration ctr = cancellationToken.Register(() =>
            _logger.Information("Preloading is requested to be cancelled"));

        _logger.Debug(
            "Tip before preloading: #{TipIndex} {TipHash}",
            Blockchain.Tip.Height,
            Blockchain.Tip.BlockHash);

        // FIXME: Currently `IProgress<PreloadState>` can be rewinded to the previous stage
        // as it starts from the first stage when it's still not close enough to the topmost
        // tip in the network.
        for (int i = 0; !cancellationToken.IsCancellationRequested; i++)
        {
            _logger.Information(
                "Fetching excerpts from {PeersCount} peers...",
                Peers.Count);
            var peersWithExcerpts = await GetPeersWithExcerpts(
                dialTimeout, int.MaxValue, cancellationToken);

            if (!peersWithExcerpts.Any())
            {
                _logger.Information("There are no appropriate peers for preloading");
                break;
            }
            else
            {
                _logger.Information(
                    "Fetched {PeersWithExcerptsCount} excerpts from {PeersCount} peers",
                    peersWithExcerpts.Count,
                    Peers.Count);
            }

            Block localTip = Blockchain.Tip;
            BlockExcerpt topmostTip = peersWithExcerpts
                .Select(pair => pair.Item2)
                .Aggregate((prev, next) => prev.Height > next.Height ? prev : next);
            if (topmostTip.Height - (i > 0 ? tipDeltaThreshold : 0L) <= localTip.Height)
            {
                const string msg =
                    "As the local tip (#{LocalTipIndex} {LocalTipHash}) is close enough to " +
                    "the topmost tip in the network (#{TopmostTipIndex} {TopmostTipHash}), " +
                    "preloading is no longer needed";
                _logger.Information(
                    msg,
                    localTip.Height,
                    localTip.BlockHash,
                    topmostTip.Height,
                    topmostTip.BlockHash);
                break;
            }
            else
            {
                const string msg =
                    "As the local tip (#{LocalTipIndex} {LocalTipHash}) is still not close " +
                    "enough to the topmost tip in the network " +
                    "(#{TopmostTipIndex} {TopmostTipHash}), preload one more time...";
                _logger.Information(
                    msg,
                    localTip.Height,
                    localTip.BlockHash,
                    topmostTip.Height,
                    topmostTip.BlockHash);
            }

            _logger.Information("Preloading (trial #{Trial}) started...", i + 1);

            BlockCandidateTable.Cleanup((_) => true);
            await PullBlocksAsync(
                peersWithExcerpts,
                cancellationToken);

            await ConsumeBlockCandidates(
                cancellationToken: cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Use <see cref="FindNeighborsMessage"/> messages to find a <see cref="Peer"/> with
    /// <see cref="Address"/> of <paramref name="target"/>.
    /// </summary>
    /// <param name="target">The <see cref="Address"/> to find.</param>
    /// <param name="depth">Target depth of recursive operation. If -1 is given,
    /// will recursive until the closest <see cref="Peer"/> to the
    /// <paramref name="target"/> is found.</param>
    /// <param name="timeout">
    /// <see cref="TimeSpan"/> for waiting reply of <see cref="FindNeighborsMessage"/>.
    /// If <see langword="null"/> is given, <see cref="TimeoutException"/> will not be thrown.
    /// </param>
    /// <param name="cancellationToken">A cancellation token used to propagate notification
    /// that this operation should be canceled.</param>
    /// <returns>
    /// A <see cref="Peer"/> with <see cref="Address"/> of <paramref name="target"/>.
    /// Returns <see langword="null"/> if the peer with address does not exist.
    /// </returns>
    public async Task<Peer> FindSpecificPeerAsync(
        Address target,
        int depth = 3,
        CancellationToken cancellationToken = default)
    {
        Kademlia kademliaProtocol = (Kademlia)PeerDiscovery;
        return await kademliaProtocol.FindSpecificPeerAsync(
            target,
            depth,
            cancellationToken);
    }

    /// <summary>
    /// Validates all <see cref="Peer"/>s in the routing table by sending a simple message.
    /// </summary>
    /// <param name="timeout">Timeout for this operation. If it is set to
    /// <see langword="null"/>, wait infinitely until the requested operation is finished.
    /// </param>
    /// <param name="cancellationToken">A cancellation token used to propagate notification
    /// that this operation should be canceled.</param>
    /// <returns>An awaitable task without value.</returns>
    public async Task CheckAllPeersAsync(
        CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource cts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, _cancellationToken);
        cancellationToken = cts.Token;

        Kademlia kademliaProtocol = (Kademlia)PeerDiscovery;
        await kademliaProtocol.CheckAllPeersAsync(cancellationToken);
    }

    /// <summary>
    /// Adds <paramref name="peers"/> to routing table by sending a simple message.
    /// </summary>
    /// <param name="peers">A list of peers to add.</param>
    /// <param name="timeout">Timeout for this operation. If it is set to
    /// <see langword="null"/>, wait infinitely until the requested operation is finished.
    /// </param>
    /// <param name="cancellationToken">A cancellation token used to propagate notification
    /// that this operation should be canceled.</param>
    /// <returns>An awaitable task without value.</returns>
    public Task AddPeersAsync(
        IEnumerable<Peer> peers,
        CancellationToken cancellationToken = default)
    {
        if (Transport is null)
        {
            throw new ArgumentNullException(nameof(Transport));
        }

        if (cancellationToken == default)
        {
            cancellationToken = _cancellationToken;
        }

        return PeerDiscovery.AddPeersAsync(peers, cancellationToken);
    }

    // FIXME: This would be better if it's merged with GetDemandBlockHashes
    internal async Task<List<BlockHash>> GetBlockHashes(
        Peer peer,
        BlockHash blockHash,
        CancellationToken cancellationToken = default)
    {
        var request = new GetBlockHashesMessage { BlockHash = blockHash };

        const string sendMsg =
            "Sending a {MessageType} message with locator [{LocatorHead}]";
        _logger.Debug(
            sendMsg,
            nameof(GetBlockHashesMessage),
            blockHash);

        MessageEnvelope parsedMessage;
        try
        {
            parsedMessage = await Transport.SendMessageAsync(
                peer,
                request,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CommunicationException)
        {
            _logger.Debug(
                "Failed to get a response for " + nameof(GetBlockHashesMessage) +
                " due to a communication failure");
            return new List<BlockHash>();
        }

        if (parsedMessage.Message is BlockHashesMessage blockHashes)
        {
            if (blockHashes.Hashes.Any())
            {
                if (blockHash.Equals(blockHashes.Hashes.First()))
                {
                    List<BlockHash> hashes = blockHashes.Hashes.ToList();
                    _logger.Debug(
                        "Received a " + nameof(BlockHashesMessage) + " with {Length} hashes",
                        hashes.Count);
                    return hashes;
                }
                else
                {
                    const string msg =
                        "Received a " + nameof(BlockHashesMessage) + " but its " +
                        "first hash {ActualBlockHash} does not match " +
                        "the locator hash {ExpectedBlockHash}";
                    _logger.Debug(msg, blockHashes.Hashes.First(), blockHash);
                    return new List<BlockHash>();
                }
            }
            else
            {
                const string msg =
                    "Received a " + nameof(BlockHashesMessage) + " with zero hashes";
                _logger.Debug(msg);
                return new List<BlockHash>();
            }
        }
        else
        {
            _logger.Debug(
                "A response for " + nameof(GetBlockHashesMessage) +
                " is expected to be {ExpectedType}: {ReceivedType}",
                nameof(BlockHashesMessage),
                parsedMessage.GetType());
            return new List<BlockHash>();
        }
    }

    /// <summary>
    /// Download <see cref="Block"/>s corresponding to <paramref name="blockHashes"/>
    /// from <paramref name="peer"/>.
    /// </summary>
    /// <param name="peer">A <see cref="Peer"/> to request <see cref="Block"/>s from.
    /// </param>
    /// <param name="blockHashes">A <see cref="List{T}"/> of <see cref="BlockHash"/>es
    /// of <see cref="Block"/>s to be downloaded from <paramref name="peer"/>.</param>
    /// <param name="cancellationToken">A cancellation token used to propagate notification
    /// that this operation should be canceled.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="Block"/> and
    /// <see cref="BlockCommit"/> pairs corresponding to <paramref name="blockHashes"/>.
    /// Returned <see cref="Block"/>s are guaranteed to correspond to the initial part of
    /// <paramref name="blockHashes"/>, including the empty list and the full list in order.
    /// </returns>
    /// <exception cref="InvalidMessageContractException">Thrown when
    /// a message other than <see cref="BlocksMessage"/> is received while
    /// trying to get <see cref="Block"/>s from <paramref name="peer"/>.</exception>
    internal async IAsyncEnumerable<(Block, BlockCommit)> GetBlocksAsync(
        Peer peer,
        List<BlockHash> blockHashes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.Information(
            "Trying to download {BlockHashesCount} block(s) from {Peer}...",
            blockHashes.Count,
            peer);

        var request = new GetBlocksMessage { BlockHashes = [.. blockHashes] };
        int hashCount = blockHashes.Count;

        if (hashCount < 1)
        {
            yield break;
        }

        TimeSpan blockRecvTimeout = Options.TimeoutOptions.GetBlocksBaseTimeout
            + Options.TimeoutOptions.GetBlocksPerBlockHashTimeout.Multiply(hashCount);
        if (blockRecvTimeout > Options.TimeoutOptions.MaxTimeout)
        {
            blockRecvTimeout = Options.TimeoutOptions.MaxTimeout;
        }

        var messageEnvelope = await Transport.SendMessageAsync(peer, request, cancellationToken);
        var aggregateMessage = (AggregateMessage)messageEnvelope.Message;

        _logger.Debug("Received replies from {Peer}", peer);
        int count = 0;

        foreach (var message in aggregateMessage.Messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (message is BlocksMessage blockMessage)
            {
                var payloads = blockMessage.Payloads;
                _logger.Information(
                    "Received {Count} blocks from {Peer}",
                    payloads.Length,
                    messageEnvelope.Peer);
                for (int i = 0; i < payloads.Length; i += 2)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    byte[] blockPayload = payloads[i];
                    byte[] commitPayload = payloads[i + 1];
                    Block block = ModelSerializer.DeserializeFromBytes<Block>(blockPayload);
                    BlockCommit commit = commitPayload.Length == 0
                        ? default
                        : ModelSerializer.DeserializeFromBytes<BlockCommit>(commitPayload);

                    if (count < blockHashes.Count)
                    {
                        if (blockHashes[count].Equals(block.BlockHash))
                        {
                            yield return (block, commit);
                            count++;
                        }
                        else
                        {
                            _logger.Debug(
                                "Expected a block with hash {ExpectedBlockHash} but " +
                                "received a block with hash {ActualBlockHash}",
                                blockHashes[count],
                                block.BlockHash);
                            yield break;
                        }
                    }
                    else
                    {
                        _logger.Debug(
                            "Expected to receive {BlockCount} blocks but " +
                            "received more blocks than expected",
                            blockHashes.Count);
                        yield break;
                    }
                }
            }
            else
            {
                string errorMessage =
                    $"Expected a {nameof(BlocksMessage)} message as a response of " +
                    $"the {nameof(GetBlocksMessage)} message, but got a {message.GetType().Name} " +
                    $"message instead: {message}";
                throw new InvalidMessageContractException(errorMessage);
            }
        }

        _logger.Information("Downloaded {Count} block(s) from {Peer}", count, peer);
    }

    internal async IAsyncEnumerable<Transaction> GetTxsAsync(
        Peer peer,
        IEnumerable<TxId> txIds,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var txIdsAsArray = txIds as TxId[] ?? txIds.ToArray();
        var request = new GetTransactionMessage { TxIds = [.. txIdsAsArray] };
        int txCount = txIdsAsArray.Count();

        _logger.Debug("Required tx count: {Count}", txCount);

        var txRecvTimeout = Options.TimeoutOptions.GetTxsBaseTimeout
            + Options.TimeoutOptions.GetTxsPerTxIdTimeout.Multiply(txCount);
        if (txRecvTimeout > Options.TimeoutOptions.MaxTimeout)
        {
            txRecvTimeout = Options.TimeoutOptions.MaxTimeout;
        }

        var messageEnvelope = await Transport.SendMessageAsync(peer, request, cancellationToken);
        var aggregateMessage = (AggregateMessage)messageEnvelope.Message;

        foreach (var message in aggregateMessage.Messages)
        {
            if (message is TransactionMessage parsed)
            {
                Transaction tx = ModelSerializer.DeserializeFromBytes<Transaction>(parsed.Payload);
                yield return tx;
            }
            else
            {
                string errorMessage =
                    $"Expected {nameof(Transaction)} messages as response of " +
                    $"the {nameof(GetTransactionMessage)} message, but got a {message.GetType().Name} " +
                    $"message instead: {message}";
                throw new InvalidMessageContractException(errorMessage);
            }
        }
    }

    /// <summary>
    /// Gets all <see cref="BlockHash"/>es for <see cref="Block"/>s needed to be downloaded
    /// by querying <see cref="Peer"/>s.
    /// </summary>
    /// <param name="blockChain">The <see cref="Blockchain"/> to use as a reference
    /// for generating a <see cref="BlockLocator"/> when querying.  This may not necessarily
    /// be <see cref="Blockchain"/>, the canonical <see cref="Blockchain"/> instance held
    /// by this <see cref="Swarm"/> instance.</param>
    /// <param name="peersWithExcerpts">The <see cref="List{T}"/> of <see cref="Peer"/>s
    /// to query with their tips known.</param>
    /// <param name="cancellationToken">The cancellation token that should be used to propagate
    /// a notification that this operation should be canceled.</param>
    /// <returns>An <see cref="List{T}"/> of <see cref="BlockHash"/>es together with
    /// its source <see cref="Peer"/>.  This is guaranteed to always return a non-empty
    /// <see cref="List{T}"/> unless an <see cref="Exception"/> is thrown.</returns>
    /// <exception cref="AggregateException">Thrown when failed to download
    /// <see cref="BlockHash"/>es from a <see cref="Peer"/>.</exception>
    /// <remarks>
    /// <para>
    /// This method uses the tip information for each <see cref="Peer"/> provided with
    /// <paramref name="peersWithExcerpts"/> whether to make a query in the first place.
    /// </para>
    /// <para>
    /// Returned list of tuples is simply the first successful query result from a
    /// <see cref="Peer"/> among <paramref name="peersWithExcerpts"/>.
    /// </para>
    /// <para>
    /// This implicitly assumes returned <see cref="BlockHashesMessage"/> is properly
    /// indexed with a valid branching <see cref="BlockHash"/> as its first element and
    /// skips it when constructing the result as it is not necessary to download.
    /// As such, returned result is simply a "dump" of possible <see cref="BlockHash"/>es
    /// to download.
    /// </para>
    /// </remarks>
    internal async Task<(Peer, List<BlockHash>)> GetDemandBlockHashes(
        Blockchain blockChain,
        IList<(Peer, BlockExcerpt)> peersWithExcerpts,
        CancellationToken cancellationToken = default)
    {
        var exceptions = new List<Exception>();
        foreach ((Peer peer, BlockExcerpt excerpt) in peersWithExcerpts)
        {
            if (!IsBlockNeeded(excerpt))
            {
                _logger.Verbose(
                    "Skip peer {Peer} because its block excerpt is not needed",
                    Peers);
                continue;
            }

            try
            {
                List<BlockHash> downloadedHashes = await GetDemandBlockHashesFromPeer(
                    blockChain,
                    peer,
                    excerpt,
                    cancellationToken);
                if (downloadedHashes.Any())
                {
                    return (peer, downloadedHashes);
                }
                else
                {
                    continue;
                }
            }
            catch (Exception e)
            {
                const string message =
                    "Failed to fetch demand block hashes from {Peer}; " +
                    "retry with another peer...";
                _logger.Debug(e, message, peer);
                exceptions.Add(e);
                continue;
            }
        }

        Peer[] peers = peersWithExcerpts.Select(p => p.Item1).ToArray();
        _logger.Warning(
            "Failed to fetch demand block hashes from peers: {Peers}",
            peers);
        throw new AggregateException(
            "Failed to fetch demand block hashes from peers: " +
            string.Join(", ", peers.Select(p => p.ToString())),
            exceptions);
    }

    internal async Task<List<BlockHash>> GetDemandBlockHashesFromPeer(
        Blockchain blockChain,
        Peer peer,
        BlockExcerpt excerpt,
        CancellationToken cancellationToken = default)
    {
        var blockHash = blockChain.Tip.BlockHash;
        long peerIndex = excerpt.Height;
        var downloaded = new List<BlockHash>();

        try
        {
            _logger.Verbose(
                "Request block hashes to {Peer} (height: {PeerHeight}) using " +
                "locator [{LocatorHead}]",
                peer,
                peerIndex,
                blockHash);

            List<BlockHash> blockHashes = await GetBlockHashes(
                peer: peer,
                blockHash: blockHash,
                cancellationToken: cancellationToken);

            foreach (var item in blockHashes)
            {
                _logger.Verbose(
                    "Received a block hash from {Peer}: {BlockHash}",
                    peer,
                    item);
                downloaded.Add(item);
            }

            return downloaded;
        }
        catch (Exception e)
        {
            _logger.Error(
                e,
                "Failed to fetch demand block hashes from {Peer}",
                peer);
            throw new Exception("Failed");
        }
    }

    private void BroadcastBlock(Address except, Block block)
    {
        _logger.Information(
            "Trying to broadcast block #{Index} {Hash}...",
            block.Height,
            block.BlockHash);
        var message = new BlockHeaderMessage { GenesisHash = Blockchain.Genesis.BlockHash, Excerpt = block };
        BroadcastMessage(except, message);
    }

    private void BroadcastTxs(Peer except, IEnumerable<Transaction> txs)
    {
        List<TxId> txIds = txs.Select(tx => tx.Id).ToList();
        _logger.Information("Broadcasting {Count} txIds...", txIds.Count);
        BroadcastTxIds(except.Address, txIds);
    }

    private void BroadcastMessage(Address except, MessageBase message)
    {
        Transport.BroadcastMessage(
            RoutingTable.PeersToBroadcast(except, Options.MinimumBroadcastTarget),
            message);
    }

    /// <summary>
    /// Gets <see cref="BlockExcerpt"/>es from randomly selected <see cref="Peer"/>s
    /// from <see cref="Peers"/> with each <see cref="BlockExcerpt"/> tied to
    /// its originating <see cref="Peer"/>.
    /// </summary>
    /// <param name="dialTimeout">Timeout for each dialing operation to
    /// a <see cref="Peer"/> in <see cref="Peers"/>.  Not having a timeout limit
    /// is equivalent to setting this value to <see langword="null"/>.</param>
    /// <param name="maxPeersToDial">Maximum number of <see cref="Peer"/>s to dial.</param>
    /// <param name="cancellationToken">A cancellation token used to propagate notification
    /// that this operation should be canceled.</param>
    /// <returns>An awaitable task with a <see cref="List{T}"/> of tuples
    /// of <see cref="Peer"/> and <see cref="BlockExcerpt"/> ordered randomly.</returns>
    private async Task<List<(Peer, BlockExcerpt)>> GetPeersWithExcerpts(
        TimeSpan? dialTimeout,
        int maxPeersToDial,
        CancellationToken cancellationToken)
    {
        Random random = new Random();
        Block tip = Blockchain.Tip;
        BlockHash genesisHash = Blockchain.Genesis.BlockHash;
        return (await DialExistingPeers(dialTimeout, maxPeersToDial, cancellationToken))
            .Where(
                pair => pair.Item2 is { } chainStatus &&
                    genesisHash.Equals(chainStatus.GenesisHash) &&
                    chainStatus.TipIndex > tip.Height)
            .Select(pair => (pair.Item1, (BlockExcerpt)pair.Item2))
            .OrderBy(_ => random.Next())
            .ToList();
    }

    /// <summary>
    /// Gets <see cref="ChainStatusMessage"/>es from randomly selected <see cref="Peer"/>s
    /// from <see cref="Peers"/> with each <see cref="ChainStatusMessage"/> tied to
    /// its originating <see cref="Peer"/>.
    /// </summary>
    /// <param name="dialTimeout">Timeout for each dialing operation to
    /// a <see cref="Peer"/> in <see cref="Peers"/>.  Not having a timeout limit
    /// is equivalent to setting this value to <see langword="null"/>.</param>
    /// <param name="maxPeersToDial">Maximum number of <see cref="Peer"/>s to dial.</param>
    /// <param name="cancellationToken">A cancellation token used to propagate notification
    /// that this operation should be canceled.</param>
    /// <returns>An awaitable task with an <see cref="Array"/> of tuples
    /// of <see cref="Peer"/> and <see cref="ChainStatusMessage"/> where
    /// <see cref="ChainStatusMessage"/> can be <see langword="null"/> if dialing fails for
    /// a selected <see cref="Peer"/>.</returns>
    private Task<(Peer, ChainStatusMessage)[]> DialExistingPeers(
        TimeSpan? dialTimeout,
        int maxPeersToDial,
        CancellationToken cancellationToken)
    {
        // FIXME: It would be better if it returns IAsyncEnumerable<(BoundPeer, ChainStatus)>
        // instead.
        void LogException(Peer peer, Task<MessageEnvelope> task)
        {
            switch (task.Exception?.InnerException)
            {
                case CommunicationException cfe:
                    _logger.Debug(
                        cfe,
                        "Failed to dial {Peer}",
                        peer);
                    break;
                case Exception e:
                    _logger.Error(
                        e, "An unexpected exception occurred while dialing {Peer}", peer);
                    break;
                default:
                    break;
            }
        }

        var rnd = new System.Random();
        IEnumerable<Task<(Peer, ChainStatusMessage)>> tasks = Peers.OrderBy(_ => rnd.Next())
            .Take(maxPeersToDial)
            .Select(
                peer => Transport.SendMessageAsync(
                    peer,
                    new GetChainStatusMessage(),
                    cancellationToken)
                .ContinueWith<(Peer, ChainStatusMessage)>(
                    task =>
                    {
                        if (task.IsFaulted || task.IsCanceled ||
                            !(task.Result.Message is ChainStatusMessage chainStatus))
                        {
                            // Log and mark to skip
                            LogException(peer, task);
                            return (peer, null);
                        }
                        else
                        {
                            return (peer, chainStatus);
                        }
                    },
                    cancellationToken));

        return Task.WhenAll(tasks).ContinueWith(
            task =>
            {
                if (task.IsFaulted)
                {
                    throw task.Exception;
                }

                return task.Result.ToArray();
            },
            cancellationToken);
    }

    private async Task BroadcastBlockAsync(
        TimeSpan broadcastBlockInterval,
        CancellationToken cancellationToken)
    {
        const string fname = nameof(BroadcastBlockAsync);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(broadcastBlockInterval, cancellationToken);
                BroadcastBlock(Blockchain.Tip);
            }
            catch (OperationCanceledException e)
            {
                _logger.Warning(e, "{MethodName}() was canceled", fname);
                throw;
            }
            catch (Exception e)
            {
                _logger.Error(
                    e, "An unexpected exception occurred during {MethodName}()", fname);
            }
        }
    }

    private async Task BroadcastTxAsync(
        TimeSpan broadcastTxInterval,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(broadcastTxInterval, cancellationToken);

                await Task.Run(
                    () =>
                    {
                        List<TxId> txIds = Blockchain
                            .StagedTransactions.Keys
                            .ToList();

                        if (txIds.Any())
                        {
                            _logger.Debug(
                                "Broadcasting {TxCount} staged transactions...",
                                txIds.Count);
                            BroadcastTxIds(default, txIds);
                        }
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException e)
            {
                _logger.Warning(e, "{MethodName}() was canceled", nameof(BroadcastTxAsync));
                throw;
            }
            catch (Exception e)
            {
                _logger.Error(
                    e,
                    "An unexpected exception occurred during {MethodName}()",
                    nameof(BroadcastTxAsync));
            }
        }
    }

    private void BroadcastTxIds(Address except, IEnumerable<TxId> txIds)
    {
        var message = new TxIdsMessage { Ids = [.. txIds] };
        BroadcastMessage(except, message);
    }

    /// <summary>
    /// Checks if the corresponding <see cref="Block"/> to a given
    /// <see cref="BlockExcerpt"/> is needed for <see cref="Blockchain"/>.
    /// </summary>
    /// <param name="target">The <see cref="BlockExcerpt"/> to compare to the current
    /// <see cref="Blockchain.Tip"/> of <see cref="Blockchain"/>.</param>
    /// <returns><see langword="true"/> if the corresponding <see cref="Block"/> to
    /// <paramref name="target"/> is needed, otherwise, <see langword="false"/>.</returns>
    private bool IsBlockNeeded(BlockExcerpt target)
    {
        return target.Height > Blockchain.Tip.Height;
    }

    private async Task RefreshTableAsync(
        TimeSpan period,
        TimeSpan maxAge,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PeerDiscovery.RefreshTableAsync(maxAge, cancellationToken);
                await PeerDiscovery.CheckReplacementCacheAsync(cancellationToken);
                await Task.Delay(period, cancellationToken);
            }
            catch (OperationCanceledException e)
            {
                _logger.Warning(e, "{MethodName}() was cancelled", nameof(RefreshTableAsync));
                throw;
            }
            catch (Exception e)
            {
                _logger.Warning(
                    e,
                    "An unexpected exception occurred during {MethodName}()",
                    nameof(RefreshTableAsync));
            }
        }
    }

    private async Task RebuildConnectionAsync(
        TimeSpan period,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(period, cancellationToken);
                await PeerDiscovery.RebuildConnectionAsync(
                    Kademlia.MaxDepth,
                    cancellationToken);
            }
            catch (OperationCanceledException e)
            {
                _logger.Warning(e, $"{nameof(RebuildConnectionAsync)}() is cancelled");
                throw;
            }
            catch (Exception e)
            {
                _logger.Warning(
                    e,
                    "Unexpected exception occurred during {MethodName}()",
                    nameof(RebuildConnectionAsync));
            }
        }
    }

    private async Task MaintainStaticPeerAsync(
        TimeSpan period,
        CancellationToken cancellationToken)
    {
        TimeSpan timeout = TimeSpan.FromSeconds(3);
        while (!cancellationToken.IsCancellationRequested)
        {
            var tasks = Options.StaticPeers
                .Where(peer => !RoutingTable.Contains(peer))
                .Select(async peer =>
                {
                    try
                    {
                        await AddPeersAsync(new[] { peer }, cancellationToken);
                    }
                    catch (TimeoutException)
                    {
                    }
                });
            await Task.WhenAll(tasks);
            await Task.Delay(period, cancellationToken);
        }
    }

    private async Task CreateLongRunningTask(Func<Task> f)
    {
        using var thread = new AsyncContextThread();
        await thread.Factory.Run(f).WaitAsync(_cancellationToken);
    }
}
