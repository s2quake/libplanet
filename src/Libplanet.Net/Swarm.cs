using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Protocols;
using Libplanet.Types;
using Libplanet.Net.Transports;
using System.Reactive;
using System.Reactive.Subjects;
using Libplanet.Types.Threading;
using Libplanet.Net.Tasks;

namespace Libplanet.Net;

public sealed class Swarm : IAsyncDisposable
{
    private readonly Subject<Unit> _blockHeaderReceivedSubject = new();
    private readonly Subject<Unit> _blockReceivedSubject = new();
    private readonly Subject<Unit> _blockAppendedSubject = new();

    private readonly Subject<Unit> _fillBlocksAsyncStartedSubject = new();
    private readonly Subject<Unit> _fillBlocksAsyncFailedSubject = new();
    private readonly Subject<Unit> _processFillBlocksFinishedSubject = new();

    private readonly ISigner _signer;
    private readonly ConsensusReactor? _consensusReactor;
    private readonly TxFetcher _txFetcher;
    private readonly IDisposable _txFetcherSubscription;
    private readonly EvidenceFetcher _evidenceFetcher;
    private readonly IDisposable _evidenceFetcherSubscription;
    private readonly AccessLimiter _transferBlockLimiter;
    private readonly AccessLimiter _transferTxLimiter;
    private readonly AccessLimiter _transferEvidenceLimiter;
    private readonly ConcurrentDictionary<Peer, int> _processBlockDemandSessions = new();
    private readonly List<ISwarmTask> _backgroundList;

    private CancellationTokenSource? _cancellationTokenSource;
    private CancellationToken _cancellationToken;
    private Task[] _tasks = [];

    private bool _disposed;

    public Swarm(
        ISigner signer,
        Blockchain blockchain,
        SwarmOptions options,
        ConsensusReactorOptions? consensusOption = null)
    {
        _signer = signer;
        Blockchain = blockchain;
        Options = options;
        RoutingTable = new RoutingTable(Address, options.TableSize, options.BucketSize);
        Transport = new NetMQTransport(signer, options.TransportOptions);
        _txFetcher = new TxFetcher(Blockchain, Transport, options.TimeoutOptions);
        _evidenceFetcher = new EvidenceFetcher(Blockchain, Transport, options.TimeoutOptions);
        Transport.Process.Subscribe(ProcessMessageHandler);
        PeerDiscovery = new Kademlia(RoutingTable, Transport, Address);
        BlockDemandDictionary = new BlockDemandDictionary(options.BlockDemandLifespan);
        _txFetcherSubscription = _txFetcher.Received.Subscribe(e => BroadcastTxs(e.Peer, e.Items));
        _evidenceFetcherSubscription = _evidenceFetcher.Received.Subscribe(e => BroadcastEvidence(e.Peer.Address, e.Items));
        _transferBlockLimiter = new(options.TaskRegulationOptions.MaxTransferBlocksTaskCount);
        _transferTxLimiter = new(options.TaskRegulationOptions.MaxTransferTxsTaskCount);
        _transferEvidenceLimiter = new(options.TaskRegulationOptions.MaxTransferTxsTaskCount);
        _consensusReactor = consensusOption is not null ? new ConsensusReactor(signer, Blockchain, consensusOption) : null;
        _backgroundList =
        [
            new BlockBroadcastTask(this),
            new TxBroadcastTask(this),
            new EvidenceBroadcastTask(this),
            new FillBlocksTask(this),
            new PollBlocksTask(this),
            new ConsumeBlockCandidatesTask(this),
            new RefreshTableTask(this),
            new RebuildConnectionTask(this),
            new MaintainStaticPeerTask(this),
        ];
    }

    public bool IsRunning { get; private set; }

    public bool ConsensusRunning => _consensusReactor?.IsRunning ?? false;

    public Address Address => _signer.Address;

    public Peer Peer => Transport.Peer;

    public IReadOnlyList<Peer> Peers => RoutingTable.Peers;

    public ImmutableArray<Peer> Validators => _consensusReactor?.Validators ?? [];

    public Blockchain Blockchain { get; private set; }

    public Protocol Protocol => Transport.Protocol;

    internal RoutingTable RoutingTable { get; }

    internal Kademlia PeerDiscovery { get; }

    internal ITransport Transport { get; }

    internal IObservable<ReceivedInfo<Transaction>> TxReceived => _txFetcher.Received;

    internal IObservable<ReceivedInfo<EvidenceBase>> EvidenceReceived => _evidenceFetcher.Received;

    internal IObservable<Unit> BlockHeaderReceived => _blockHeaderReceivedSubject;

    internal IObservable<Unit> BlockReceived => _blockReceivedSubject;

    internal IObservable<Unit> BlockAppended => _blockAppendedSubject;

    [Obsolete("not used")]
    internal IObservable<Unit> FillBlocksAsyncStarted => _fillBlocksAsyncStartedSubject;

    [Obsolete("not used")]
    internal IObservable<Unit> FillBlocksAsyncFailed => _fillBlocksAsyncFailedSubject;

    [Obsolete("not used")]
    internal IObservable<Unit> ProcessFillBlocksFinished => _processFillBlocksFinishedSubject;

    // FIXME: We need some sort of configuration method for it.
    internal int FindNextHashesChunkSize { get; set; } = 500;

    internal SwarmOptions Options { get; }

    internal ConsensusReactor ConsensusReactor
        => _consensusReactor ?? throw new InvalidOperationException("ConsensusReactor is not initialized.");

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
            await Transport.DisposeAsync();
            if (_consensusReactor is not null)
            {
                await _consensusReactor.DisposeAsync();
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _disposed = true;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Swarm is already running.");
        }

        await PreloadAsync(cancellationToken);
        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;
        await Transport.StartAsync(cancellationToken);
        await BootstrapAsync(cancellationToken);
        if (_consensusReactor is not null)
        {
            await _consensusReactor.StartAsync(cancellationToken);
        }
        _tasks = [.. _backgroundList.Where(item => item.IsEnabled).Select(item => item.RunAsync(_cancellationToken))];
        IsRunning = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!IsRunning || _cancellationTokenSource is null)
        {
            throw new InvalidOperationException("Swarm is not running.");
        }

        await _cancellationTokenSource.CancelAsync();
        await TaskUtility.TryWaitAll(_tasks);
        _tasks = [];

        await Transport.StopAsync(cancellationToken);
        if (_consensusReactor is not null)
        {
            await _consensusReactor.StopAsync(cancellationToken);
        }

        BlockDemandDictionary = new BlockDemandDictionary(Options.BlockDemandLifespan);
        BlockCandidateTable.Cleanup(_ => true);
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
        IsRunning = false;
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

        await PeerDiscovery.BootstrapAsync(seedPeers, searchDepth, cancellationToken);

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
        TimeSpan dialTimeout, CancellationToken cancellationToken)
    {
        // FIXME: It would be better if it returns IAsyncEnumerable<PeerChainState> instead.
        return (await DialExistingPeers(dialTimeout, int.MaxValue, cancellationToken))
            .Select(pp =>
                new PeerChainState(
                    pp.Item1,
                    pp.Item2?.TipHeight ?? -1));
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
            if (peersWithExcerpts.Length == 0)
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
            // await ConsumeBlockCandidates(cancellationToken: cancellationToken);
            i++;
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<Peer> FindSpecificPeerAsync(
        Address target, int depth = 3, CancellationToken cancellationToken = default)
    {
        return await PeerDiscovery.FindSpecificPeerAsync(target, depth, cancellationToken);
    }

    public async Task CheckAllPeersAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource cts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, _cancellationToken);
        cancellationToken = cts.Token;

        Kademlia kademliaProtocol = PeerDiscovery;
        await kademliaProtocol.CheckAllPeersAsync(cancellationToken);
    }

    public async Task AddPeersAsync(ImmutableArray<Peer> peers, CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationToken);

        await PeerDiscovery.AddPeersAsync(peers, cancellationTokenSource.Token);
    }

    internal async Task<(Peer, BlockHash[])> GetDemandBlockHashes(
        Block block,
        (Peer, BlockSummary)[] peersWithSummaries,
        CancellationToken cancellationToken = default)
    {
        var exceptionList = new List<Exception>();
        foreach (var (peer, blockSummary) in peersWithSummaries)
        {
            if (!IsBlockNeeded(blockSummary))
            {
                continue;
            }

            try
            {
                var blockHashes = await Transport.GetBlockHashesAsync(peer, block.BlockHash, cancellationToken);
                if (blockHashes.Length != 0)
                {
                    return (peer, blockHashes);
                }
                else
                {
                    continue;
                }
            }
            catch (Exception e)
            {
                exceptionList.Add(e);
            }
        }

        var peers = peersWithSummaries.Select(p => p.Item1).ToArray();
        throw new AggregateException(
            "Failed to fetch demand block hashes from peers: " +
            string.Join(", ", peers.Select(p => p.ToString())),
            exceptionList);
    }

    private void BroadcastBlock(Address except, Block block)
    {
        var message = new BlockHeaderMessage
        {
            GenesisHash = Blockchain.Genesis.BlockHash,
            BlockSummary = block,
        };
        BroadcastMessage(except, message);
    }

    private void BroadcastTxs(Peer except, IEnumerable<Transaction> txs)
    {
        List<TxId> txIds = txs.Select(tx => tx.Id).ToList();
        BroadcastTxIds(except.Address, txIds);
    }

    internal void BroadcastMessage(Address except, MessageBase message)
    {
        Transport.Broadcast(
            RoutingTable.PeersToBroadcast(except, Options.MinimumBroadcastTarget),
            message);
    }

    internal async Task<(Peer, BlockSummary)[]> GetPeersWithExcerpts(
        TimeSpan dialTimeout, int maxPeersToDial, CancellationToken cancellationToken)
    {
        var random = new Random();
        var tip = Blockchain.Tip;
        var genesisHash = Blockchain.Genesis.BlockHash;
        return (await DialExistingPeers(dialTimeout, maxPeersToDial, cancellationToken))
            .Where(
                pair => pair.Item2 is { } chainStatus &&
                    genesisHash.Equals(chainStatus.GenesisHash) &&
                    chainStatus.TipHeight > tip.Height)
            .Select(pair => (pair.Item1, (BlockSummary)pair.Item2))
            .OrderBy(_ => random.Next())
            .ToArray();
    }

    private async Task<(Peer, ChainStatusMessage)[]> DialExistingPeers(
        TimeSpan dialTimeout, int maxPeersToDial, CancellationToken cancellationToken)
    {
        var peers = Peers.ToArray();
        var random = new Random();
        var transport = Transport;
        random.Shuffle(peers);
        peers = peers[..maxPeersToDial];

        var tasks = peers.Select(async item =>
        {
            var task = transport.GetChainStatusAsync(item, cancellationToken);
            return await task.WaitAsync(dialTimeout, cancellationToken);
        }).ToArray();

        await TaskUtility.TryWaitAll(tasks);
        var query = peers.Zip(tasks).Where(item => item.Second.IsCompletedSuccessfully)
            .Select(item => (item.First, item.Second.Result));

        return [.. query];
    }

    // private async Task BroadcastTxAsync(TimeSpan broadcastTxInterval, CancellationToken cancellationToken)
    // {
    //     while (!cancellationToken.IsCancellationRequested)
    //     {
    //         try
    //         {
    //             await Task.Delay(broadcastTxInterval, cancellationToken);
    //             await Task.Run(
    //                 () =>
    //                 {
    //                     var txIds = Blockchain.StagedTransactions.Keys.ToArray();
    //                     if (txIds.Length > 0)
    //                     {
    //                         BroadcastTxIds(default, txIds);
    //                     }
    //                 },
    //                 cancellationToken);
    //         }
    //         catch (OperationCanceledException)
    //         {
    //             throw;
    //         }
    //         catch
    //         {
    //             // do nothing
    //         }
    //     }

    //     cancellationToken.ThrowIfCancellationRequested();
    // }

    internal void BroadcastTxIds(Address except, IEnumerable<TxId> txIds)
    {
        var message = new TxIdMessage { Ids = [.. txIds] };
        BroadcastMessage(except, message);
    }

    internal bool IsBlockNeeded(BlockSummary blockSummary) => blockSummary.Height > Blockchain.Tip.Height;

    // private async Task RefreshTableAsync(TimeSpan period, TimeSpan maxAge, CancellationToken cancellationToken)
    // {
    //     while (!cancellationToken.IsCancellationRequested)
    //     {
    //         try
    //         {
    //             await PeerDiscovery.RefreshTableAsync(maxAge, cancellationToken);
    //             await PeerDiscovery.CheckReplacementCacheAsync(cancellationToken);
    //             await Task.Delay(period, cancellationToken);
    //         }
    //         catch (OperationCanceledException)
    //         {
    //             throw;
    //         }
    //         catch
    //         {
    //             // do nothing
    //         }
    //     }

    //     cancellationToken.ThrowIfCancellationRequested();
    // }

    // private async Task RebuildConnectionAsync(TimeSpan period, CancellationToken cancellationToken)
    // {
    //     while (!cancellationToken.IsCancellationRequested)
    //     {
    //         try
    //         {
    //             await Task.Delay(period, cancellationToken);
    //             await PeerDiscovery.RebuildConnectionAsync(Kademlia.MaxDepth, cancellationToken);
    //         }
    //         catch (OperationCanceledException)
    //         {
    //             throw;
    //         }
    //         catch
    //         {
    //             // do nothing
    //         }
    //     }

    //     cancellationToken.ThrowIfCancellationRequested();
    // }

    // private async Task MaintainStaticPeerAsync(TimeSpan period, CancellationToken cancellationToken)
    // {
    //     while (!cancellationToken.IsCancellationRequested)
    //     {
    //         var tasks = Options.StaticPeers
    //             .Where(peer => !RoutingTable.Contains(peer))
    //             .Select(async peer =>
    //             {
    //                 try
    //                 {
    //                     var timeout = TimeSpan.FromSeconds(3);
    //                     await AddPeersAsync([peer], cancellationToken).WaitAsync(timeout, cancellationToken);
    //                 }
    //                 catch (OperationCanceledException)
    //                 {
    //                     // do nothing
    //                 }
    //             });
    //         await Task.WhenAll(tasks);
    //         await Task.Delay(period, cancellationToken);
    //     }

    //     cancellationToken.ThrowIfCancellationRequested();
    // }

    private void ProcessMessageHandler(MessageEnvelope messageEnvelope)
    {
        switch (messageEnvelope.Message)
        {
            case PingMessage _:
            case GetPeerMessage _:
                return;

            case GetChainStatusMessage:
                {
                    // This is based on the assumption that genesis block always exists.
                    var tip = Blockchain.Tip;
                    var replyMessage = new ChainStatusMessage
                    {
                        ProtocolVersion = tip.Version,
                        GenesisHash = Blockchain.Genesis.BlockHash,
                        TipHeight = tip.Height,
                        TipHash = tip.BlockHash,
                    };

                    Transport.Reply(messageEnvelope.Identity, replyMessage);
                }
                break;

            case GetBlockHashesMessage getBlockHashes:
                {
                    var height = Blockchain.Blocks[getBlockHashes.BlockHash].Height;
                    var hashes = Blockchain.Blocks[height..].Select(item => item.BlockHash).ToArray();

                    // IReadOnlyList<BlockHash> hashes = BlockChain.FindNextHashes(
                    //     getBlockHashes.Locator,
                    //     FindNextHashesChunkSize);
                    var replyMessage = new BlockHashMessage { BlockHashes = [.. hashes] };

                    Transport.Reply(messageEnvelope.Identity, replyMessage);
                }
                break;

            case GetBlockMessage getBlockMessage:
                _ = TransferAsync(messageEnvelope.Identity, getBlockMessage, _cancellationToken);
                break;

            case GetTransactionMessage getTransactionMessage:
                _ = TransferAsync(messageEnvelope.Identity, getTransactionMessage, _cancellationToken);
                break;

            case GetEvidenceMessage getEvidenceMessage:
                _ = TransferAsync(messageEnvelope.Identity, getEvidenceMessage, _cancellationToken);
                break;

            case TxIdMessage txIdMessage:
                _txFetcher.DemandMany(messageEnvelope.Peer, [.. txIdMessage.Ids]);
                Transport.Pong(messageEnvelope);
                break;

            case EvidenceIdMessage evidenceIdMessage:
                _evidenceFetcher.DemandMany(messageEnvelope.Peer, [.. evidenceIdMessage.Ids]);
                Transport.Pong(messageEnvelope);
                break;

            case BlockHashMessage _:
                break;

            case BlockHeaderMessage blockHeader:
                ProcessBlockHeader(messageEnvelope);
                Transport.Pong(messageEnvelope);
                break;

            default:
                throw new InvalidOperationException($"Failed to handle message: {messageEnvelope.Message}");
        }
    }

    private void ProcessBlockHeader(MessageEnvelope messageEnvelope)
    {
        var blockHeaderMsg = (BlockHeaderMessage)messageEnvelope.Message;
        if (!blockHeaderMsg.GenesisHash.Equals(Blockchain.Genesis.BlockHash))
        {
            return;
        }

        _blockHeaderReceivedSubject.OnNext(Unit.Default);
        var header = blockHeaderMsg.BlockSummary;

        try
        {
            header.Timestamp.ValidateTimestamp();
        }
        catch (InvalidOperationException e)
        {
            return;
        }

        bool needed = IsBlockNeeded(header);
        if (needed)
        {
            BlockDemandDictionary.Add(
                IsBlockNeeded, new BlockDemand(header, messageEnvelope.Peer, DateTimeOffset.UtcNow));
        }
    }

    private async Task TransferAsync(
        Guid identity, GetBlockMessage requestMessage, CancellationToken cancellationToken)
    {
        using var scope = await _transferBlockLimiter.CanAccessAsync(cancellationToken);
        if (scope is null)
        {
            return;
        }

        var blockHashes = requestMessage.BlockHashes;
        var blockList = new List<Block>();
        var blockCommitList = new List<BlockCommit>();
        foreach (var blockHash in blockHashes)
        {
            if (Blockchain.Blocks.TryGetValue(blockHash, out var block)
                && Blockchain.BlockCommits.TryGetValue(block.BlockHash, out var blockCommit))
            {
                blockList.Add(block);
                blockCommitList.Add(blockCommit);
            }

            if (blockList.Count == requestMessage.ChunkSize)
            {
                Transport.Transfer(identity, [.. blockList], [.. blockCommitList], hasNext: true);
                blockList.Clear();
                blockCommitList.Clear();
            }
        }

        Transport.Transfer(identity, [.. blockList], [.. blockCommitList]);
    }

    private async Task TransferAsync(
        Guid identity, GetTransactionMessage requestMessage, CancellationToken cancellationToken)
    {
        using var scope = await _transferTxLimiter.CanAccessAsync(cancellationToken);
        if (scope is null)
        {
            return;
        }

        var txIds = requestMessage.TxIds;
        var txs = txIds
            .Select(txId => Blockchain.Transactions.TryGetValue(txId, out var tx) ? tx : null)
            .OfType<Transaction>()
            .ToArray();
        Transport.Transfer(identity, txs);
    }

    private async Task TransferAsync(
        Guid identity, GetEvidenceMessage requestMessage, CancellationToken cancellationToken)
    {
        using var scope = await _transferEvidenceLimiter.CanAccessAsync(cancellationToken);
        if (scope is null)
        {
            return;
        }

        var evidenceIds = requestMessage.EvidenceIds;
        var evidence = evidenceIds
            .Select(evidenceId => Blockchain.PendingEvidences.TryGetValue(evidenceId, out var ev) ? ev : null)
            .OfType<EvidenceBase>()
            .ToArray();

        Transport.Transfer(identity, evidence);
    }

    public void BroadcastEvidence(ImmutableArray<EvidenceBase> evidence) => BroadcastEvidence(default, evidence);

    private void BroadcastEvidence(Address except, ImmutableArray<EvidenceBase> evidence)
    {
        var evidenceIds = evidence.Select(evidence => evidence.Id).ToArray();
        var replyMessage = new EvidenceIdMessage { Ids = [.. evidenceIds] };
        BroadcastMessage(except, replyMessage);
    }

    public BlockDemandDictionary BlockDemandDictionary { get; private set; }

    public BlockCandidateTable BlockCandidateTable { get; } = new BlockCandidateTable();

    internal async Task PullBlocksAsync(
        TimeSpan timeout,
        int maximumPollPeers,
        CancellationToken cancellationToken)
    {
        if (maximumPollPeers <= 0)
        {
            return;
        }

        var peersWithBlockExcerpt = await GetPeersWithExcerpts(timeout, maximumPollPeers, cancellationToken);
        await PullBlocksAsync(peersWithBlockExcerpt, cancellationToken);
    }

    private async Task PullBlocksAsync(
        (Peer, BlockSummary)[] peersWithBlockExcerpt, CancellationToken cancellationToken)
    {
        if (!peersWithBlockExcerpt.Any())
        {
            return;
        }

        long totalBlocksToDownload = 0L;
        Block tempTip = Blockchain.Tip;
        var blocks = new List<(Block, BlockCommit)>();

        try
        {
            // NOTE: demandBlockHashes is always non-empty.
            (var peer, var demandBlockHashes) = await GetDemandBlockHashes(
                Blockchain.Tip, peersWithBlockExcerpt, cancellationToken);
            totalBlocksToDownload = demandBlockHashes.Length;

            var query = Transport.GetBlocksAsync(peer, demandBlockHashes, cancellationToken);

            await foreach ((Block block, BlockCommit commit) in query.WithCancellation(cancellationToken))
            {
                blocks.Add((block, commit));
            }
        }
        catch (Exception e)
        {
            _fillBlocksAsyncFailedSubject.OnNext(Unit.Default);
        }
        finally
        {
            if (totalBlocksToDownload > 0)
            {
                try
                {
                    var branch = blocks.ToImmutableSortedDictionary(item => item.Item1, item => item.Item2);
                    BlockCandidateTable.Add(Blockchain.Tip, branch);
                    _blockReceivedSubject.OnNext(Unit.Default);
                }
                catch (ArgumentException ae)
                {
                }
            }

            _processFillBlocksFinishedSubject.OnNext(Unit.Default);
        }
    }

    internal bool BlockCandidateProcess(
        ImmutableSortedDictionary<Block, BlockCommit> candidate,
        CancellationToken cancellationToken)
    {
        try
        {
            _fillBlocksAsyncStartedSubject.OnNext(Unit.Default);
            AppendBranch(
                blockChain: Blockchain,
                candidate: candidate,
                cancellationToken: cancellationToken);
            _processFillBlocksFinishedSubject.OnNext(Unit.Default);
            return true;
        }
        catch (Exception e)
        {
            _fillBlocksAsyncFailedSubject.OnNext(Unit.Default);
            return false;
        }
    }

    private void AppendBranch(
        Blockchain blockChain,
        ImmutableSortedDictionary<Block, BlockCommit> candidate,
        CancellationToken cancellationToken = default)
    {
        var oldTip = blockChain.Tip;
        var branchpoint = oldTip;
        var blocks = ExtractBlocksToAppend(branchpoint, candidate);
        var verifiedBlockCount = 0;

        foreach (var (block, commit) in blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            blockChain.Append(block, commit);
            verifiedBlockCount++;
        }
    }

    private List<(Block, BlockCommit)> ExtractBlocksToAppend(Block branchpoint, ImmutableSortedDictionary<Block, BlockCommit> branch)
    {
        var trimmed = new List<(Block, BlockCommit)>();
        bool matchFound = false;
        foreach (var (key, value) in branch)
        {
            if (matchFound)
            {
                trimmed.Add((key, value));
            }
            else
            {
                matchFound = branchpoint.BlockHash.Equals(key.BlockHash);
            }
        }

        return trimmed;
    }

    private async Task<bool> ProcessBlockDemandAsync(BlockDemand demand, CancellationToken cancellationToken)
    {
        Peer peer = demand.Peer;

        if (_processBlockDemandSessions.ContainsKey(peer))
        {
            // Another task has spawned for the peer.
            return false;
        }

        var sessionRandom = new Random();

        int sessionId = sessionRandom.Next();

        if (demand.Height <= Blockchain.Tip.Height)
        {
            return false;
        }


        try
        {
            _processBlockDemandSessions.TryAdd(peer, sessionId);
            var result = await BlockCandidateDownload(
                peer: peer,
                blockChain: Blockchain,
                logSessionId: sessionId,
                cancellationToken: cancellationToken);

            _blockReceivedSubject.OnNext(Unit.Default);
            return result;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            // Maybe demand table can be cleaned up here, but it will be eventually
            // cleaned up in FillBlocksAsync()
            _processBlockDemandSessions.TryRemove(peer, out _);
        }
    }

    private async Task<bool> BlockCandidateDownload(
        Peer peer,
        Blockchain blockChain,
        int logSessionId,
        CancellationToken cancellationToken)
    {
        var tipBlockHash = blockChain.Tip.BlockHash;
        Block tip = blockChain.Tip;

        var hashes = await Transport.GetBlockHashesAsync(peer, tipBlockHash, cancellationToken);
        if (hashes.Length == 0)
        {
            _fillBlocksAsyncFailedSubject.OnNext(Unit.Default);
            return false;
        }

        IAsyncEnumerable<(Block, BlockCommit)> blocksAsync = Transport.GetBlocksAsync(
            peer,
            hashes,
            cancellationToken);
        try
        {
            var items = await blocksAsync.ToArrayAsync(cancellationToken);
            var branch = items.ToImmutableSortedDictionary(
                item => item.Item1,
                item => item.Item2);
            BlockCandidateTable.Add(tip, branch);
            return true;
        }
        catch (ArgumentException ae)
        {
            return false;
        }
    }
}
