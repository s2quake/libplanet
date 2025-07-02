using System.Collections.Concurrent;
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
using Libplanet.Net.Transports;
using System.Reactive;
using System.Reactive.Subjects;
using Libplanet.Types.Threading;

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
    private readonly List<Task> _taskList = [];

    private CancellationTokenSource? _cancellationTokenSource;
    private CancellationToken _cancellationToken;
    private IDisposable? _tipChangedSubscription;

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
        Transport.ProcessMessage.Subscribe(ProcessMessageHandler);
        PeerDiscovery = new Kademlia(RoutingTable, Transport, Address);
        BlockDemandDictionary = new BlockDemandDictionary(options.BlockDemandLifespan);
        _txFetcherSubscription = _txFetcher.Received.Subscribe(e => BroadcastTxs(e.Peer, e.Items));
        _evidenceFetcherSubscription = _evidenceFetcher.Received.Subscribe(e => BroadcastEvidence(e.Peer, e.Items));
        _transferBlockLimiter = new(options.TaskRegulationOptions.MaxTransferBlocksTaskCount);
        _transferTxLimiter = new(options.TaskRegulationOptions.MaxTransferTxsTaskCount);
        _transferEvidenceLimiter = new(options.TaskRegulationOptions.MaxTransferTxsTaskCount);
        _consensusReactor = consensusOption is not null ? new ConsensusReactor(signer, Blockchain, consensusOption) : null;
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
            // TxCompletion?.Dispose();
            await Transport.DisposeAsync();
            await _consensusReactor.DisposeAsync();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _disposed = true;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            throw new SwarmException("Swarm is already running.");
        }

        await BootstrapAsync(cancellationToken);
        await PreloadAsync(cancellationToken);
        // var broadcastBlockInterval = Options.BlockBroadcastInterval;
        // var broadcastTxInterval = Options.TxBroadcastInterval;
        // var dialTimeout = Options.TimeoutOptions.DialTimeout;
        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;

        _tipChangedSubscription = Blockchain.TipChanged.Subscribe(OnBlockChainTipChanged);
        await Transport.StartAsync(cancellationToken);

        _taskList.AddRange(
        [
            BroadcastBlockAsync(Options.BlockBroadcastInterval, _cancellationToken),
            BroadcastTxAsync(Options.TxBroadcastInterval, _cancellationToken),
            BroadcastEvidenceAsync(Options.EvidenceBroadcastInterval, _cancellationToken),
            FillBlocksAsync(_cancellationToken),
            PollBlocksAsync(Options.TimeoutOptions.DialTimeout, Options.TipLifespan, Options.MaximumPollPeers, _cancellationToken),
            ConsumeBlockCandidates(TimeSpan.FromMilliseconds(10), _cancellationToken),
            RefreshTableAsync(Options.RefreshPeriod,Options.RefreshLifespan,_cancellationToken),
            RebuildConnectionAsync(TimeSpan.FromMinutes(30), _cancellationToken),
        ]);

        if (_consensusReactor is { })
        {
            await _consensusReactor.StartAsync(cancellationToken);
        }

        if (!Options.StaticPeers.IsEmpty)
        {
            _taskList.Add(MaintainStaticPeerAsync(Options.StaticPeersMaintainPeriod, _cancellationToken));
        }

        IsRunning = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!IsRunning || _cancellationTokenSource is null)
        {
            throw new SwarmException("Swarm is not running.");
        }

        await _cancellationTokenSource.CancelAsync();
        await TaskUtility.TryWaitAll([.. _taskList]);
        _taskList.Clear();

        _tipChangedSubscription?.Dispose();
        _tipChangedSubscription = null;

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

    private void ProcessMessageHandler(MessageEnvelope messageEnvelope)
    {
        switch (messageEnvelope.Message)
        {
            case PingMessage _:
            case FindNeighborsMessage _:
                return;

            case GetChainStatusMessage:
                {
                    // This is based on the assumption that genesis block always exists.
                    Block tip = Blockchain.Tip;
                    var chainStatus = new ChainStatusMessage
                    {
                        ProtocolVersion = tip.Version,
                        GenesisHash = Blockchain.Genesis.BlockHash,
                        TipIndex = tip.Height,
                        TipHash = tip.BlockHash,
                    };

                    Transport.ReplyMessage(messageEnvelope.Identity, chainStatus);
                }
                break;

            case GetBlockHashesMessage getBlockHashes:
                {
                    var height = Blockchain.Blocks[getBlockHashes.BlockHash].Height;
                    var hashes = Blockchain.Blocks[height..].Select(item => item.BlockHash).ToArray();

                    // IReadOnlyList<BlockHash> hashes = BlockChain.FindNextHashes(
                    //     getBlockHashes.Locator,
                    //     FindNextHashesChunkSize);
                    var reply = new BlockHashesMessage { Hashes = [.. hashes] };

                    Transport.ReplyMessage(messageEnvelope.Identity, reply);
                }
                break;

            case GetBlocksMessage:
                TransferBlocksAsync(messageEnvelope);
                break;

            case GetTransactionMessage getTransactionMessage:
                _ = TransferTxsAsync(messageEnvelope.Identity, getTransactionMessage, _cancellationToken);
                break;

            case GetEvidenceMessage getTxs:
                _ = TransferEvidenceAsync(messageEnvelope, _cancellationToken);
                break;

            case TxIdsMessage txIds:
                ProcessTxIds(messageEnvelope);
                Transport.Pong(messageEnvelope);
                break;

            case EvidenceIdsMessage evidenceIds:
                ProcessEvidenceIds(messageEnvelope);
                Transport.Pong(messageEnvelope);
                break;

            case BlockHashesMessage _:
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
        var header = blockHeaderMsg.Excerpt;

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

    private async Task TransferTxsAsync(
        Guid identity, GetTransactionMessage requestMessage, CancellationToken cancellationToken)
    {
        using var scope = await _transferTxLimiter.WaitAsync(cancellationToken);
        if (scope is null)
        {
            return;
        }

        foreach (var txId in requestMessage.TxIds)
        {
            if (!Blockchain.Transactions.TryGetValue(txId, out var tx))
            {
                continue;
            }

            var replyMessage = new TransactionMessage
            {
                Payload = [.. ModelSerializer.SerializeToBytes(tx)],
            };
            Transport.ReplyMessage(identity, replyMessage);
        }
    }

    private void ProcessTxIds(MessageEnvelope messageEnvelope)
    {
        var txIdsMsg = (TxIdsMessage)messageEnvelope.Message;
        _txFetcher.DemandMany(messageEnvelope.Peer, [.. txIdsMsg.Ids]);
    }

    private async Task TransferBlocksAsync(MessageEnvelope message)
    {
        using var scope = await _transferBlockLimiter.WaitAsync(_cancellationToken);
        if (scope is null)
        {
            return;
        }

        var blocksMsg = (GetBlocksMessage)message.Message;
        // string reqId = !(message.Id is null) && message.Id.Length == 16
        //     ? new Guid(message.Id).ToString()
        //     : "unknown";
        // _logger.Verbose(
        //     "Preparing a {MessageType} message to reply to {Identity}...",
        //     nameof(Messages.BlocksMessage),
        //     reqId);

        var payloads = new List<byte[]>();

        List<BlockHash> hashes = blocksMsg.BlockHashes.ToList();
        int count = 0;
        int total = hashes.Count;
        const string logMsg =
            "Fetching block {Index}/{Total} {Hash} to include in " +
            "a reply to {Identity}...";
        foreach (BlockHash hash in hashes)
        {
            // _logger.Verbose(logMsg, count, total, hash, reqId);
            if (Blockchain.Blocks.TryGetValue(hash, out var block))
            {
                byte[] blockPayload = ModelSerializer.SerializeToBytes(block);
                payloads.Add(blockPayload);
                byte[] commitPayload = Blockchain.BlockCommits[block.BlockHash] is { } commit
                    ? ModelSerializer.SerializeToBytes(commit)
                    : Array.Empty<byte>();
                payloads.Add(commitPayload);
                count++;
            }

            if (payloads.Count / 2 == blocksMsg.ChunkSize)
            {
                var response = new BlocksMessage { Payloads = [.. payloads] };
                Transport.ReplyMessage(message.Identity, response);
                payloads.Clear();
            }
        }

        if (payloads.Any())
        {
            var response = new BlocksMessage { Payloads = [.. payloads] };
            // _logger.Verbose(
            //     "Enqueuing a blocks reply (...{Count}/{Total}) to {Identity}...",
            //     count,
            //     total,
            //     reqId);
            Transport.ReplyMessage(message.Identity, response);
        }

        if (count == 0)
        {
            var response = new BlocksMessage { Payloads = [.. payloads] };
            // _logger.Verbose(
            //     "Enqueuing a blocks reply (...{Index}/{Total}) to {Identity}...",
            //     count,
            //     total,
            //     reqId);
            Transport.ReplyMessage(message.Identity, response);
        }

        // _logger.Debug("{Count} blocks were transferred to {Identity}", count, reqId);

    }

    public void BroadcastEvidence(IEnumerable<EvidenceBase> evidence)
    {
        BroadcastEvidence(null, evidence);
    }

    internal async IAsyncEnumerable<EvidenceBase> GetEvidenceAsync(
        Peer peer,
        IEnumerable<EvidenceId> evidenceIds,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var evidenceIdsAsArray = evidenceIds as EvidenceId[] ?? evidenceIds.ToArray();
        var request = new GetEvidenceMessage { EvidenceIds = [.. evidenceIdsAsArray] };
        int evidenceCount = evidenceIdsAsArray.Count();

        var evidenceRecvTimeout = Options.TimeoutOptions.GetTxsBaseTimeout
            + Options.TimeoutOptions.GetTxsPerTxIdTimeout.Multiply(evidenceCount);
        if (evidenceRecvTimeout > Options.TimeoutOptions.MaxTimeout)
        {
            evidenceRecvTimeout = Options.TimeoutOptions.MaxTimeout;
        }

        var messageEnvelope = await Transport.SendMessageAsync(peer, request, cancellationToken);
        var aggregateMessage = (AggregateMessage)messageEnvelope.Message;

        foreach (var message in aggregateMessage.Messages)
        {
            if (message is EvidenceMessage parsed)
            {
                EvidenceBase evidence = ModelSerializer.DeserializeFromBytes<EvidenceBase>([.. parsed.Payload]);
                yield return evidence;
            }
            else
            {
                string errorMessage =
                    $"Expected {nameof(Transaction)} messages as response of " +
                    $"the {nameof(GetEvidenceMessage)} message, but got a " +
                    $"{message.GetType().Name} " +
                    $"message instead: {message}";
                throw new InvalidOperationException(errorMessage);
            }
        }
    }

    private void BroadcastEvidence(Peer? except, IEnumerable<EvidenceBase> evidence)
    {
        List<EvidenceId> evidenceIds = evidence.Select(evidence => evidence.Id).ToList();
        BroadcastEvidenceIds(except?.Address ?? default, evidenceIds);
    }

    private async Task BroadcastEvidenceAsync(
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
                        List<EvidenceId> evidenceIds = Blockchain.PendingEvidences.Keys.ToList();

                        if (evidenceIds.Any())
                        {
                            BroadcastEvidenceIds(default, evidenceIds);
                        }
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException e)
            {
                throw;
            }
            catch (Exception e)
            {
            }
        }
    }

    private void BroadcastEvidenceIds(Address except, IEnumerable<EvidenceId> evidenceIds)
    {
        var message = new EvidenceIdsMessage { Ids = [.. evidenceIds] };
        BroadcastMessage(except, message);
    }

    private async Task TransferEvidenceAsync(MessageEnvelope message, CancellationToken cancellationToken)
    {
        using var scope = await _transferEvidenceLimiter.WaitAsync(cancellationToken);
        if (scope is null)
        {
            return;
        }

        var getEvidenceMsg = (GetEvidenceMessage)message.Message;
        foreach (EvidenceId txid in getEvidenceMsg.EvidenceIds)
        {
            EvidenceBase? ev = Blockchain.PendingEvidences[txid];

            if (ev is null)
            {
                continue;
            }

            MessageBase response = new EvidenceMessage
            {
                Payload = [.. ModelSerializer.SerializeToBytes(ev)],
            };
            Transport.ReplyMessage(message.Identity, response);
        }
    }

    private void ProcessEvidenceIds(MessageEnvelope message)
    {
        var evidenceIdsMsg = (EvidenceIdsMessage)message.Message;
        // EvidenceCompletion.Demand(message.Peer, evidenceIdsMsg.Ids);
        _evidenceFetcher.DemandMany(message.Peer, [.. evidenceIdsMsg.Ids]);
    }

    public BlockDemandDictionary BlockDemandDictionary { get; private set; }

    public BlockCandidateTable BlockCandidateTable { get; } = new BlockCandidateTable();

    internal async Task PullBlocksAsync(
        TimeSpan? timeout,
        int maximumPollPeers,
        CancellationToken cancellationToken)
    {
        if (maximumPollPeers <= 0)
        {
            return;
        }

        List<(Peer, BlockExcerpt)> peersWithBlockExcerpt =
            await GetPeersWithExcerpts(
                timeout, maximumPollPeers, cancellationToken);
        await PullBlocksAsync(peersWithBlockExcerpt, cancellationToken);
    }

    private async Task PullBlocksAsync(
        List<(Peer, BlockExcerpt)> peersWithBlockExcerpt,
        CancellationToken cancellationToken)
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
                Blockchain,
                peersWithBlockExcerpt,
                cancellationToken);
            totalBlocksToDownload = demandBlockHashes.Length;

            var downloadedBlocks = GetBlocksAsync(
                peer,
                demandBlockHashes,
                cancellationToken);

            await foreach (
                (Block block, BlockCommit commit) in
                    downloadedBlocks.WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                blocks.Add((block, commit));
            }
        }
        catch (Exception e)
        {
            var msg =
                $"Unexpected exception occurred during {nameof(PullBlocksAsync)}()";
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

    private async Task FillBlocksAsync(CancellationToken cancellationToken)
    {
        var checkInterval = TimeSpan.FromMilliseconds(100);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (BlockDemandDictionary.Count > 0)
            {
                foreach (var blockDemand in BlockDemandDictionary.Values)
                {
                    BlockDemandDictionary.Remove(blockDemand.Peer);
                    _ = ProcessBlockDemandAsync(blockDemand, cancellationToken);
                }
            }
            else
            {
                await Task.Delay(checkInterval, cancellationToken);
                continue;
            }

            BlockDemandDictionary.Cleanup(IsBlockNeeded);
        }
    }

    private async Task PollBlocksAsync(
        TimeSpan timeout,
        TimeSpan tipLifespan,
        int maximumPollPeers,
        CancellationToken cancellationToken)
    {
        BlockExcerpt lastTip = Blockchain.Tip;
        DateTimeOffset lastUpdated = DateTimeOffset.UtcNow;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!lastTip.BlockHash.Equals(Blockchain.Tip.BlockHash))
            {
                lastUpdated = DateTimeOffset.UtcNow;
                lastTip = Blockchain.Tip;
            }
            else if (lastUpdated + tipLifespan < DateTimeOffset.UtcNow)
            {
                await PullBlocksAsync(
                    timeout, maximumPollPeers, cancellationToken);
            }

            await Task.Delay(1000, cancellationToken);
        }
    }

    private void OnBlockChainTipChanged(TipChangedInfo e)
    {
        if (IsRunning)
        {
            BroadcastBlock(e.Tip);
        }
    }

    private async Task ConsumeBlockCandidates(
        TimeSpan? checkInterval = null,
        CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (BlockCandidateTable.Count > 0)
            {
                BlockHeader tipHeader = Blockchain.Tip.Header;
                if (BlockCandidateTable.GetCurrentRoundCandidate(Blockchain.Tip) is { } branch)
                {
                    var root = branch.Keys.First();
                    var tip = branch.Keys.Last();
                    _ = BlockCandidateProcess(
                        branch,
                        cancellationToken);
                    _blockAppendedSubject.OnNext(Unit.Default);
                }
            }
            else if (checkInterval is { } interval)
            {
                await Task.Delay(interval, cancellationToken);
                continue;
            }
            else
            {
                break;
            }

            BlockCandidateTable.Cleanup(IsBlockNeeded);
        }
    }

    private bool BlockCandidateProcess(
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

        var hashes = await GetBlockHashes(
            peer: peer,
            blockHash: tipBlockHash,
            cancellationToken: cancellationToken);

        if (hashes.Length == 0)
        {
            _fillBlocksAsyncFailedSubject.OnNext(Unit.Default);
            return false;
        }

        IAsyncEnumerable<(Block, BlockCommit)> blocksAsync = GetBlocksAsync(
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
