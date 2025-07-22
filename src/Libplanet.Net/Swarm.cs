using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Protocols;
using Libplanet.Types;
using Libplanet.Net.NetMQ;
using System.Reactive;
using System.Reactive.Subjects;
using Libplanet.Types.Threading;
using Libplanet.Net.Tasks;
using Libplanet.Net.MessageHandlers;
using System.Runtime.CompilerServices;
using Nito.AsyncEx.Synchronous;

namespace Libplanet.Net;

public sealed class Swarm : ServiceBase, IServiceProvider
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
    private readonly AccessLimiter _transferEvidenceLimiter;
    private readonly ServicesCollection _services;
    private readonly IMessageHandler[] _messageHandlers;

    public Swarm(
        ISigner signer,
        Blockchain blockchain,
        SwarmOptions options,
        ConsensusReactorOptions? consensusOption = null)
    {
        _signer = signer;
        Blockchain = blockchain;
        Options = options;
        RoutingTable = new RoutingTable(signer.Address);
        Transport = new NetMQTransport(signer, options.TransportOptions);
        _txFetcher = new TxFetcher(Blockchain, Transport, options.TimeoutOptions);
        _evidenceFetcher = new EvidenceFetcher(Blockchain, Transport, options.TimeoutOptions);
        // Transport.Process.Subscribe(ProcessMessageHandler);
        PeerDiscovery = new PeerDiscovery(RoutingTable, Transport);
        BlockDemandDictionary = new BlockDemandDictionary(options.BlockDemandLifespan);
        _txFetcherSubscription = _txFetcher.Received.Subscribe(e => BroadcastTxs(e.Peer, e.Items));
        _evidenceFetcherSubscription = _evidenceFetcher.Received.Subscribe(e => BroadcastEvidence(e.Peer.Address, e.Items));
        _transferEvidenceLimiter = new(options.TaskRegulationOptions.MaxTransferTxsTaskCount);
        _consensusReactor = consensusOption is not null ? new ConsensusReactor(signer, Blockchain, consensusOption) : null;

        _services =
        [
            new BlockBroadcastTask(this),
            new TxBroadcastTask(this),
            new EvidenceBroadcastTask(this),
            new FillBlocksTask(this),
            new PollBlocksTask(this),
            new ConsumeBlockCandidatesTask(this),
            new RefreshTableTask(PeerDiscovery, options.RefreshPeriod, options.RefreshLifespan),
            new RebuildConnectionTask(this),
            new MaintainStaticPeerTask(this),
        ];
        _messageHandlers =
        [
            new BlockRequestMessageHandler(this, options),
            new BlockHashRequestMessageHandler(this),
            new TransactionRequestMessageHandler(this, options),
            new ChainStatusRequestMessageHandler(this),
        ];
        Transport.MessageHandlers.AddRange(_messageHandlers);
    }

    public bool ConsensusRunning => _consensusReactor?.IsRunning ?? false;

    public Address Address => _signer.Address;

    public Peer Peer => Transport.Peer;

    public IEnumerable<Peer> Peers => RoutingTable.Select(item => item.Peer);

    public ImmutableArray<Peer> Validators => _consensusReactor?.Validators ?? [];

    public Blockchain Blockchain { get; private set; }

    internal RoutingTable RoutingTable { get; }

    internal PeerDiscovery PeerDiscovery { get; }

    internal ITransport Transport { get; }

    internal IObservable<ReceivedInfo<Transaction>> TxReceived => _txFetcher.Received;

    internal IObservable<ReceivedInfo<EvidenceBase>> EvidenceReceived => _evidenceFetcher.Received;

    internal IObservable<Unit> BlockHeaderReceived => _blockHeaderReceivedSubject;

    internal IObservable<Unit> BlockReceived => _blockReceivedSubject;

    internal IObservable<Unit> BlockAppended => _blockAppendedSubject;

    // FIXME: We need some sort of configuration method for it.
    internal int FindNextHashesChunkSize { get; set; } = 500;

    internal SwarmOptions Options { get; }

    internal ConsensusReactor ConsensusReactor
        => _consensusReactor ?? throw new InvalidOperationException("ConsensusReactor is not initialized.");

    private async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        if (!Options.BootstrapOptions.Enabled)
        {
            return;
        }

        var seedPeers = Options.BootstrapOptions.SeedPeers;
        var searchDepth = Options.BootstrapOptions.SearchDepth;
        if (seedPeers.Count > 0)
        {
            await PeerDiscovery.BootstrapAsync(seedPeers, searchDepth, cancellationToken);
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

    // public async Task<BlockchainState[]> GetBlockchainStateAsync(
    //     TimeSpan dialTimeout, CancellationToken cancellationToken)
    // {
    //     return await GetBlockchainStateAsync(dialTimeout, int.MaxValue, cancellationToken);
    // }

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
            var peersWithExcerpts = await GetPeersWithBlockSummary(dialTimeout, cancellationToken)
                .ToArrayAsync(cancellationToken);
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

    public Task<Peer> FindPeerAsync(Address address, CancellationToken cancellationToken)
        => FindPeerAsync(address, depth: 3, cancellationToken);

    public Task<Peer> FindPeerAsync(Address address, int depth, CancellationToken cancellationToken)
        => PeerDiscovery.FindPeerAsync(address, depth, cancellationToken);

    public async Task AddPeersAsync(ImmutableArray<Peer> peers, CancellationToken cancellationToken)
    {
        using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);

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

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        await PreloadAsync(cancellationToken);
        await Transport.StartAsync(cancellationToken);
        await BootstrapAsync(cancellationToken);
        if (_consensusReactor is not null)
        {
            await _consensusReactor.StartAsync(cancellationToken);
        }

        await _services.StartAsync(cancellationToken);
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        await _services.StopAsync(cancellationToken);
        await Transport.StopAsync(cancellationToken);
        if (_consensusReactor is not null)
        {
            await _consensusReactor.StopAsync(cancellationToken);
        }

        BlockDemandDictionary = new BlockDemandDictionary(Options.BlockDemandLifespan);
        BlockCandidateTable.Cleanup(_ => true);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        Transport.MessageHandlers.RemoveRange(_messageHandlers);
        _transferEvidenceLimiter.Dispose();

        _txFetcher.Dispose();
        _evidenceFetcher.Dispose();
        _txFetcherSubscription.Dispose();
        _evidenceFetcherSubscription.Dispose();
        await _services.DisposeAsync();
        await Transport.DisposeAsync();
        if (_consensusReactor is not null)
        {
            await _consensusReactor.DisposeAsync();
        }

        await base.DisposeAsyncCore();
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
        Transport.Post(
            RoutingTable.PeersToBroadcast(except, Options.MinimumBroadcastTarget),
            message);
    }

    internal async IAsyncEnumerable<(Peer, BlockSummary)> GetPeersWithBlockSummary(
        TimeSpan dialTimeout,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var blockchainState in GetBlockchainStateAsync(dialTimeout, cancellationToken))
        {
            if (blockchainState.Genesis.BlockHash == Blockchain.Genesis.BlockHash &&
                blockchainState.Tip.Height > Blockchain.Tip.Height)
            {
                yield return (blockchainState.Peer, blockchainState.Tip);
            }
        }
        // var random = new Random();
        // var tip = Blockchain.Tip;
        // var genesisHash = Blockchain.Genesis.BlockHash;
        // return (await GetBlockchainStateAsync(dialTimeout, maxPeersToDial, cancellationToken))
        //     .Where(item =>
        //             genesisHash.Equals(item.Genesis.BlockHash) &&
        //             item.Tip.Height > tip.Height)
        //     .Select(item => (item.Peer, item.Tip))
        //     .OrderBy(_ => random.Next())
        //     .ToArray();
    }

    // private async Task<BlockchainState[]> GetBlockchainStateAsync(
    //     TimeSpan dialTimeout, int maxPeersToDial, CancellationToken cancellationToken)
    // {
    //     var peers = Peers.ToArray();
    //     var random = new Random();
    //     var transport = Transport;
    //     random.Shuffle(peers);
    //     peers = [.. peers.Take(maxPeersToDial)];

    //     var tasks = peers.Select(async item =>
    //     {
    //         var task = transport.GetBlockchainStateAsync(item, cancellationToken);
    //         return await task.WaitAsync(dialTimeout, cancellationToken);
    //     }).ToArray();

    //     await TaskUtility.TryWhenAll(tasks);
    //     var query = peers.Zip(tasks).Where(item => item.Second.IsCompletedSuccessfully)
    //         .Select(item => Create(item.First, item.Second.Result));

    //     return [.. query];

    //     static BlockchainState Create(Peer peer, BlockchainStateResponseMessage message)
    //         => new(peer, message.Genesis, message.Tip);
    // }

    internal async IAsyncEnumerable<BlockchainState> GetBlockchainStateAsync(
        TimeSpan dialTimeout,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var peers = Peers.ToArray();
        var random = new Random();
        var transport = Transport;
        random.Shuffle(peers);

        foreach (var peer in peers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var task = transport.GetBlockchainStateAsync(peer, cancellationToken);
            var waitTask = task.WaitAsync(dialTimeout, cancellationToken);
            if (await TaskUtility.TryWait(waitTask))
            {
                yield return Create(peer, waitTask.Result);
            }
        }

        static BlockchainState Create(Peer peer, BlockchainStateResponseMessage message)
            => new(peer, message.Genesis, message.Tip);
    }

    internal void BroadcastTxIds(Address except, IEnumerable<TxId> txIds)
    {
        var message = new TxIdMessage { Ids = [.. txIds] };
        BroadcastMessage(except, message);
    }

    internal bool IsBlockNeeded(BlockSummary blockSummary) => blockSummary.Height > Blockchain.Tip.Height;

    // private void ProcessMessageHandler(IReplyContext replyContext)
    // {
    //     switch (replyContext.Message)
    //     {
    //         case PingMessage _:
    //         case GetPeerMessage _:
    //             return;

    //         case GetChainStatusMessage:
    //             {
    //                 // This is based on the assumption that genesis block always exists.
    //                 var tip = Blockchain.Tip;
    //                 var replyMessage = new ChainStatusMessage
    //                 {
    //                     ProtocolVersion = tip.Version,
    //                     GenesisHash = Blockchain.Genesis.BlockHash,
    //                     TipHeight = tip.Height,
    //                     TipHash = tip.BlockHash,
    //                 };

    //                 replyContext.NextAsync(replyMessage);
    //             }
    //             break;

    //         case GetBlockHashesMessage getBlockHashes:
    //             {
    //                 var height = Blockchain.Blocks[getBlockHashes.BlockHash].Height;
    //                 var hashes = Blockchain.Blocks[height..].Select(item => item.BlockHash).ToArray();

    //                 // IReadOnlyList<BlockHash> hashes = BlockChain.FindNextHashes(
    //                 //     getBlockHashes.Locator,
    //                 //     FindNextHashesChunkSize);
    //                 var replyMessage = new BlockHashMessage { BlockHashes = [.. hashes] };

    //                 replyContext.NextAsync(replyMessage);
    //             }
    //             break;

    //         case GetBlockMessage getBlockMessage:
    //             _ = TransferAsync(replyContext, getBlockMessage);
    //             break;

    //         case GetTransactionMessage getTransactionMessage:
    //             _ = TransferAsync(replyContext, getTransactionMessage);
    //             break;

    //         case GetEvidenceMessage getEvidenceMessage:
    //             _ = TransferAsync(replyContext, getEvidenceMessage);
    //             break;

    //         case TxIdMessage txIdMessage:
    //             _txFetcher.DemandMany(replyContext.Sender, [.. txIdMessage.Ids]);
    //             replyContext.PongAsync();
    //             break;

    //         case EvidenceIdMessage evidenceIdMessage:
    //             _evidenceFetcher.DemandMany(replyContext.Sender, [.. evidenceIdMessage.Ids]);
    //             replyContext.PongAsync();
    //             break;

    //         case BlockHashMessage _:
    //             break;

    //         case BlockHeaderMessage blockHeader:
    //             ProcessBlockHeader(replyContext);
    //             replyContext.PongAsync();
    //             break;

    //         default:
    //             throw new InvalidOperationException($"Failed to handle message: {replyContext.Message}");
    //     }
    // }

    // private void ProcessBlockHeader(IReplyContext messageEnvelope)
    // {
    //     var blockHeaderMsg = (BlockHeaderMessage)messageEnvelope.Message;
    //     if (!blockHeaderMsg.GenesisHash.Equals(Blockchain.Genesis.BlockHash))
    //     {
    //         return;
    //     }

    //     _blockHeaderReceivedSubject.OnNext(Unit.Default);
    //     var header = blockHeaderMsg.BlockSummary;

    //     try
    //     {
    //         header.Timestamp.ValidateTimestamp();
    //     }
    //     catch (InvalidOperationException e)
    //     {
    //         return;
    //     }

    //     bool needed = IsBlockNeeded(header);
    //     if (needed)
    //     {
    //         BlockDemandDictionary.Add(
    //             IsBlockNeeded, new BlockDemand(header, messageEnvelope.Sender, DateTimeOffset.UtcNow));
    //     }
    // }

    // private async Task TransferAsync(IReplyContext replyContext, GetBlockMessage requestMessage)
    // {
    //     using var cancellationTokenSource = CreateCancellationTokenSource();
    //     using var scope = await _transferBlockLimiter.CanAccessAsync(cancellationTokenSource.Token);
    //     if (scope is null)
    //     {
    //         return;
    //     }

    //     var blockHashes = requestMessage.BlockHashes;
    //     var blockList = new List<Block>();
    //     var blockCommitList = new List<BlockCommit>();
    //     foreach (var blockHash in blockHashes)
    //     {
    //         if (Blockchain.Blocks.TryGetValue(blockHash, out var block)
    //             && Blockchain.BlockCommits.TryGetValue(block.BlockHash, out var blockCommit))
    //         {
    //             blockList.Add(block);
    //             blockCommitList.Add(blockCommit);
    //         }

    //         if (blockList.Count == requestMessage.ChunkSize)
    //         {
    //             replyContext.TransferAsync([.. blockList], [.. blockCommitList], hasNext: true);
    //             blockList.Clear();
    //             blockCommitList.Clear();
    //         }
    //     }

    //     replyContext.TransferAsync([.. blockList], [.. blockCommitList]);
    // }

    // private async Task TransferAsync(
    //     IReplyContext replyContext, GetTransactionMessage requestMessage)
    // {
    //     using var cancellationTokenSource = CreateCancellationTokenSource();
    //     using var scope = await _transferTxLimiter.CanAccessAsync(cancellationTokenSource.Token);
    //     if (scope is null)
    //     {
    //         return;
    //     }

    //     var txIds = requestMessage.TxIds;
    //     var txs = txIds
    //         .Select(txId => Blockchain.Transactions.TryGetValue(txId, out var tx) ? tx : null)
    //         .OfType<Transaction>()
    //         .ToArray();
    //     replyContext.TransferAsync(txs);
    // }

    // private async Task TransferAsync(IReplyContext replyContext, GetEvidenceMessage requestMessage)
    // {
    //     using var cancellationTokenSource = CreateCancellationTokenSource();
    //     using var scope = await _transferEvidenceLimiter.CanAccessAsync(cancellationTokenSource.Token);
    //     if (scope is null)
    //     {
    //         return;
    //     }

    //     var evidenceIds = requestMessage.EvidenceIds;
    //     var evidence = evidenceIds
    //         .Select(evidenceId => Blockchain.PendingEvidences.TryGetValue(evidenceId, out var ev) ? ev : null)
    //         .OfType<EvidenceBase>()
    //         .ToArray();

    //     replyContext.TransferAsync(evidence);
    // }

    public void BroadcastEvidence(ImmutableArray<EvidenceBase> evidence) => BroadcastEvidence(default, evidence);

    private void BroadcastEvidence(Address except, ImmutableArray<EvidenceBase> evidence)
    {
        var evidenceIds = evidence.Select(evidence => evidence.Id).ToArray();
        var replyMessage = new EvidenceIdMessage { Ids = [.. evidenceIds] };
        BroadcastMessage(except, replyMessage);
    }

    public BlockDemandDictionary BlockDemandDictionary { get; private set; }

    public BlockCandidateTable BlockCandidateTable { get; } = new BlockCandidateTable();

    internal async Task PullBlocksAsync(TimeSpan timeout, int maximumPollPeers, CancellationToken cancellationToken)
    {
        if (maximumPollPeers <= 0)
        {
            return;
        }

        var peersWithBlockExcerpt = await GetPeersWithBlockSummary(timeout, cancellationToken)
            .Take(maximumPollPeers)
            .ToArrayAsync(cancellationToken);
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
                    // logging
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
                blockchain: Blockchain,
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
        Blockchain blockchain,
        ImmutableSortedDictionary<Block, BlockCommit> candidate,
        CancellationToken cancellationToken = default)
    {
        var oldTip = blockchain.Tip;
        var branchpoint = oldTip;
        var blocks = ExtractBlocksToAppend(branchpoint, candidate);
        var verifiedBlockCount = 0;

        foreach (var (block, commit) in blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            blockchain.Append(block, commit);
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

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(ITransport))
        {
            return Transport;
        }

        if (serviceType == typeof(ConsensusReactor))
        {
            return _consensusReactor;
        }

        if (serviceType == typeof(PeerDiscovery))
        {
            return PeerDiscovery;
        }

        if (serviceType == typeof(Blockchain))
        {
            return Blockchain;
        }

        foreach (var service in _services)
        {
            if (serviceType.IsInstanceOfType(service))
            {
                return service;
            }
        }

        return null;
    }
}
