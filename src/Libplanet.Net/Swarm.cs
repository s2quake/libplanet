using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Protocols;
using Libplanet.Serialization;
using Libplanet.Types;
using Nito.AsyncEx;
using System.ServiceModel;
using Libplanet.Extensions;

namespace Libplanet.Net;

public sealed partial class Swarm : IAsyncDisposable
{
    private readonly ISigner _signer;
    private readonly ConsensusReactor _consensusReactor;
    private readonly TxFetcher _txFetcher;
    private readonly IDisposable _txFetcherSubscription;
    private readonly EvidenceFetcher _evidenceFetcher;
    private readonly IDisposable _evidenceFetcherSubscription;
    private readonly AccessLimiter _transferBlockLimiter;
    private readonly AccessLimiter _transferTxLimiter;
    private readonly AccessLimiter _transferEvidenceLimiter;

    private CancellationTokenSource? _cancellationTokenSource;
    private CancellationToken _cancellationToken;
    private IDisposable? _tipChangedSubscription;

    private bool _disposed;

    public Swarm(
        Blockchain blockchain,
        PrivateKey privateKey,
        ITransport transport,
        SwarmOptions? options = null,
        ITransport? consensusTransport = null,
        ConsensusReactorOptions? consensusOption = null)
    {
        Blockchain = blockchain;
        _signer = privateKey.AsSigner();
        LastSeenTimestamps = new ConcurrentDictionary<Peer, DateTimeOffset>();
        BlockHeaderReceived = new AsyncAutoResetEvent();
        BlockAppended = new AsyncAutoResetEvent();
        BlockReceived = new AsyncAutoResetEvent();
        Options = options ?? new SwarmOptions();
        RoutingTable = new RoutingTable(Address, Options.TableSize, Options.BucketSize);

        // FIXME: after the initialization of NetMQTransport is fully converted to asynchronous
        // code, the portion initializing the swarm in Agent.cs in NineChronicles should be
        // fixed. for context, refer to
        // https://github.com/planetarium/libplanet/discussions/2303.
        Transport = transport;
        _txFetcher = new TxFetcher(Blockchain, Transport, Options.TimeoutOptions);
        _evidenceFetcher = new EvidenceFetcher(Blockchain, Transport, Options.TimeoutOptions);
        _processBlockDemandSessions = new ConcurrentDictionary<Peer, int>();
        Transport.ProcessMessage.Subscribe(ProcessMessageHandler);
        PeerDiscovery = new Kademlia(RoutingTable, Transport, Address);
        BlockDemandTable = new BlockDemandDictionary(Options.BlockDemandLifespan);
        BlockCandidateTable = new BlockCandidateTable();
        _txFetcherSubscription = _txFetcher.Received.Subscribe(e => BroadcastTxs(e.Peer, e.Items));
        _evidenceFetcherSubscription = _evidenceFetcher.Received.Subscribe(e => BroadcastEvidence(e.Peer, e.Items));

        // Regulate heavy tasks. Treat negative value as 0.
        _transferBlockLimiter = new(Options.TaskRegulationOptions.MaxTransferBlocksTaskCount);
        _transferTxLimiter = new(Options.TaskRegulationOptions.MaxTransferTxsTaskCount);
        _transferEvidenceLimiter = new(Options.TaskRegulationOptions.MaxTransferTxsTaskCount);

        // Initialize consensus reactor.
        if (consensusTransport is { } && consensusOption is { } consensusReactorOption)
        {
            _consensusReactor = new ConsensusReactor(
                privateKey.AsSigner(), consensusTransport, Blockchain, consensusReactorOption);
        }
    }

    public bool IsRunning { get; private set; }

    public bool ConsensusRunning => _consensusReactor?.IsRunning ?? false;

    public Address Address => _signer.Address;

    public Peer Peer => Transport.Peer;

    public IDictionary<Peer, DateTimeOffset> LastSeenTimestamps { get; private set; }

    public IReadOnlyList<Peer> Peers => RoutingTable.Peers;

    public ImmutableArray<Peer> Validators => _consensusReactor?.Validators ?? [];

    public Blockchain Blockchain { get; private set; }

    public Protocol Protocol => Transport.Protocol;

    internal RoutingTable RoutingTable { get; }

    internal Kademlia PeerDiscovery { get; }

    internal ITransport Transport { get; }

    internal IObservable<ReceivedInfo<Transaction>> TxReceived => _txFetcher.Received;

    internal IObservable<ReceivedInfo<EvidenceBase>> EvidenceReceived => _evidenceFetcher.Received;

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
            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync();
            }

            _transferEvidenceLimiter.Dispose();
            _transferTxLimiter.Dispose();
            _transferBlockLimiter.Dispose();

            _txFetcher.Dispose();
            _evidenceFetcher.Dispose();
            _txFetcherSubscription.Dispose();
            _evidenceFetcherSubscription.Dispose();
            // TxCompletion?.Dispose();
            await Transport.DisposeAsync();
            await _consensusReactor.DisposeAsync();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _disposed = true;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!IsRunning)
        {
            throw new SwarmException("Swarm is not running.");
        }

        _tipChangedSubscription?.Dispose();
        _tipChangedSubscription = null;

        await Transport.StopAsync(cancellationToken);
        if (_consensusReactor is not null)
        {
            await _consensusReactor.StopAsync(cancellationToken);
        }

        BlockDemandTable = new BlockDemandDictionary(Options.BlockDemandLifespan);
        BlockCandidateTable = new BlockCandidateTable();
        IsRunning = false;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            throw new SwarmException("Swarm is already running.");
        }
        //     await StartAsync(
        //         Options.TimeoutOptions.DialTimeout,
        //         Options.BlockBroadcastInterval,
        //         Options.TxBroadcastInterval,
        //         cancellationToken);
        // }

        // public async Task StartAsync(
        //     TimeSpan dialTimeout,
        //     TimeSpan broadcastBlockInterval,
        //     TimeSpan broadcastTxInterval,
        //     CancellationToken cancellationToken = default)
        // {

        await BootstrapAsync(cancellationToken);
        await PreloadAsync(cancellationToken);
        var broadcastBlockInterval = Options.BlockBroadcastInterval;
        var broadcastTxInterval = Options.TxBroadcastInterval;
        var dialTimeout = Options.TimeoutOptions.DialTimeout;
        Task<Task> runner;
        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;

        _tipChangedSubscription = Blockchain.TipChanged.Subscribe(OnBlockChainTipChanged);
        await Transport.StartAsync(cancellationToken);

        var tasks = new List<Func<Task>>
        {
            () => BroadcastBlockAsync(broadcastBlockInterval, _cancellationToken),
            () => BroadcastTxAsync(broadcastTxInterval, _cancellationToken),
            () => FillBlocksAsync(_cancellationToken),
            () => PollBlocksAsync(dialTimeout,Options.TipLifespan,Options.MaximumPollPeers,_cancellationToken),
            () => ConsumeBlockCandidates(TimeSpan.FromMilliseconds(10), _cancellationToken),
            () => RefreshTableAsync(Options.RefreshPeriod,Options.RefreshLifespan,_cancellationToken),
            () => RebuildConnectionAsync(TimeSpan.FromMinutes(30), _cancellationToken),
            () => BroadcastEvidenceAsync(broadcastTxInterval, _cancellationToken),
        };

        if (_consensusReactor is { })
        {
            await _consensusReactor.StartAsync(cancellationToken);
        }

        if (Options.StaticPeers.Any())
        {
            tasks.Add(() => MaintainStaticPeerAsync(Options.StaticPeersMaintainPeriod, _cancellationToken));
        }

        runner = Task.WhenAny(tasks.Select(CreateLongRunningTask));


        try
        {
            await await runner;
        }
        catch (OperationCanceledException e)
        {
            throw;
        }
        catch (Exception e)
        {
            throw;
        }

        IsRunning = true;
    }

    private async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        if (!Options.BootstrapOptions.Enabled)
        {
            return;
        }

        var seedPeers = Options.BootstrapOptions.SeedPeers;
        var searchDepth = Options.BootstrapOptions.SearchDepth;

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

    public void BroadcastBlock(Block block)
    {
        BroadcastBlock(default, block);
    }

    public void BroadcastTxs(IEnumerable<Transaction> txs)
    {
        BroadcastTxs(null, txs);
    }

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

    private async Task PreloadAsync(CancellationToken cancellationToken)
    {
        if (!Options.PreloadOptions.Enabled)
        {
            return;
        }

        using CancellationTokenRegistration ctr = cancellationToken.Register(() => { });

        var dialTimeout = Options.PreloadOptions.DialTimeout;
        var tipDeltaThreshold = Options.PreloadOptions.TipDeltaThreshold;

        // FIXME: Currently `IProgress<PreloadState>` can be rewinded to the previous stage
        // as it starts from the first stage when it's still not close enough to the topmost
        // tip in the network.
        var i = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var peersWithExcerpts = await GetPeersWithExcerpts(dialTimeout, int.MaxValue, cancellationToken);
            if (peersWithExcerpts.Count == 0)
            {
                break;
            }

            var tip = Blockchain.Tip;
            var topmostTip = peersWithExcerpts
                .Select(pair => pair.Item2)
                .Aggregate((prev, next) => prev.Height > next.Height ? prev : next);
            if (topmostTip.Height - (i > 0 ? tipDeltaThreshold : 0L) <= tip.Height)
            {
                break;
            }

            BlockCandidateTable.Cleanup((_) => true);
            await PullBlocksAsync(peersWithExcerpts, cancellationToken);
            await ConsumeBlockCandidates(cancellationToken: cancellationToken);
            i++;
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<Peer> FindSpecificPeerAsync(
        Address target,
        int depth = 3,
        CancellationToken cancellationToken = default)
    {
        Kademlia kademliaProtocol = PeerDiscovery;
        return await kademliaProtocol.FindSpecificPeerAsync(
            target,
            depth,
            cancellationToken);
    }

    public async Task CheckAllPeersAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource cts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, _cancellationToken);
        cancellationToken = cts.Token;

        Kademlia kademliaProtocol = PeerDiscovery;
        await kademliaProtocol.CheckAllPeersAsync(cancellationToken);
    }

    public async Task AddPeersAsync(IEnumerable<Peer> peers, CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationToken);

        await PeerDiscovery.AddPeersAsync(peers, cancellationTokenSource.Token);
    }

    internal async Task<Transaction[]> FetchTxAsync(Peer peer, TxId[] txids, CancellationToken cancellationToken)
    {
        var task = _txFetcher.Received.WaitAsync(cancellationToken);
        _txFetcher.DemandMany(peer, txids);
        var result = await task;
        return [.. result.Items];
    }

    // FIXME: This would be better if it's merged with GetDemandBlockHashes
    internal async Task<BlockHash[]> GetBlockHashes(
        Peer peer,
        BlockHash blockHash,
        CancellationToken cancellationToken = default)
    {
        var request = new GetBlockHashesMessage { BlockHash = blockHash };
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
            return [];
        }

        if (parsedMessage.Message is BlockHashesMessage blockHashes)
        {
            if (blockHashes.Hashes.Any() && blockHash.Equals(blockHashes.Hashes.First()))
            {
                return [.. blockHashes.Hashes];
            }
        }

        return [];
    }

    internal async IAsyncEnumerable<(Block, BlockCommit)> GetBlocksAsync(
        Peer peer,
        BlockHash[] blockHashes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new GetBlocksMessage { BlockHashes = [.. blockHashes] };
        int hashCount = blockHashes.Length;

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

        int count = 0;

        foreach (var message in aggregateMessage.Messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (message is BlocksMessage blockMessage)
            {
                var payloads = blockMessage.Payloads;
                for (int i = 0; i < payloads.Length; i += 2)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    byte[] blockPayload = payloads[i];
                    byte[] commitPayload = payloads[i + 1];
                    Block block = ModelSerializer.DeserializeFromBytes<Block>(blockPayload);
                    BlockCommit commit = commitPayload.Length == 0
                        ? default
                        : ModelSerializer.DeserializeFromBytes<BlockCommit>(commitPayload);

                    if (count < blockHashes.Length)
                    {
                        if (blockHashes[count].Equals(block.BlockHash))
                        {
                            yield return (block, commit);
                            count++;
                        }
                        else
                        {
                            yield break;
                        }
                    }
                    else
                    {
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
    }

    internal async Task<(Peer, BlockHash[])> GetDemandBlockHashes(
        Blockchain blockChain,
        IList<(Peer, BlockExcerpt)> peersWithExcerpts,
        CancellationToken cancellationToken = default)
    {
        var exceptions = new List<Exception>();
        foreach ((Peer peer, BlockExcerpt excerpt) in peersWithExcerpts)
        {
            if (!IsBlockNeeded(excerpt))
            {
                continue;
            }

            try
            {
                var downloadedHashes = await GetDemandBlockHashesFromPeer(
                    blockChain,
                    peer,
                    excerpt,
                    cancellationToken);
                if (downloadedHashes.Length != 0)
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
                exceptions.Add(e);
                continue;
            }
        }

        Peer[] peers = peersWithExcerpts.Select(p => p.Item1).ToArray();
        throw new AggregateException(
            "Failed to fetch demand block hashes from peers: " +
            string.Join(", ", peers.Select(p => p.ToString())),
            exceptions);
    }

    internal async Task<BlockHash[]> GetDemandBlockHashesFromPeer(
        Blockchain blockChain,
        Peer peer,
        BlockExcerpt excerpt,
        CancellationToken cancellationToken = default)
    {
        var blockHashList = new List<BlockHash>();
        var blockHashes = await GetBlockHashes(
            peer: peer,
            blockHash: blockChain.Tip.BlockHash,
            cancellationToken: cancellationToken);

        foreach (var blockHash in blockHashes)
        {
            blockHashList.Add(blockHash);
        }

        return [.. blockHashList];
    }

    private void BroadcastBlock(Address except, Block block)
    {
        var message = new BlockHeaderMessage { GenesisHash = Blockchain.Genesis.BlockHash, Excerpt = block };
        BroadcastMessage(except, message);
    }

    private void BroadcastTxs(Peer except, IEnumerable<Transaction> txs)
    {
        List<TxId> txIds = txs.Select(tx => tx.Id).ToList();
        BroadcastTxIds(except.Address, txIds);
    }

    private void BroadcastMessage(Address except, MessageBase message)
    {
        Transport.BroadcastMessage(
            RoutingTable.PeersToBroadcast(except, Options.MinimumBroadcastTarget),
            message);
    }

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
                    break;
                case Exception e:
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

    private async Task BroadcastBlockAsync(TimeSpan broadcastBlockInterval, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(broadcastBlockInterval, cancellationToken);
                BroadcastBlock(Blockchain.Tip);
            }
            catch (OperationCanceledException e)
            {
                throw;
            }
            catch
            {
                // do nothing
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task BroadcastTxAsync(TimeSpan broadcastTxInterval, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(broadcastTxInterval, cancellationToken);
                await Task.Run(
                    () =>
                    {
                        var txIds = Blockchain.StagedTransactions.Keys.ToArray();
                        if (txIds.Length > 0)
                        {
                            BroadcastTxIds(default, txIds);
                        }
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // do nothing
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private void BroadcastTxIds(Address except, IEnumerable<TxId> txIds)
    {
        var message = new TxIdsMessage { Ids = [.. txIds] };
        BroadcastMessage(except, message);
    }

    private bool IsBlockNeeded(BlockExcerpt target) => target.Height > Blockchain.Tip.Height;

    private async Task RefreshTableAsync(TimeSpan period, TimeSpan maxAge, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PeerDiscovery.RefreshTableAsync(maxAge, cancellationToken);
                await PeerDiscovery.CheckReplacementCacheAsync(cancellationToken);
                await Task.Delay(period, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // do nothing
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task RebuildConnectionAsync(TimeSpan period, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(period, cancellationToken);
                await PeerDiscovery.RebuildConnectionAsync(Kademlia.MaxDepth, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // do nothing
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task MaintainStaticPeerAsync(TimeSpan period, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var tasks = Options.StaticPeers
                .Where(peer => !RoutingTable.Contains(peer))
                .Select(async peer =>
                {
                    try
                    {
                        var timeout = TimeSpan.FromSeconds(3);
                        await AddPeersAsync([peer], cancellationToken).WaitAsync(timeout, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // do nothing
                    }
                });
            await Task.WhenAll(tasks);
            await Task.Delay(period, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task CreateLongRunningTask(Func<Task> f)
    {
        using var thread = new AsyncContextThread();
        await thread.Factory.Run(f).WaitAsync(_cancellationToken);
    }
}
