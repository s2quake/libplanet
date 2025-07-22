using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Protocols;
using Libplanet.Types;
using Libplanet.Net.NetMQ;
using Libplanet.Net.Tasks;
using Libplanet.Net.MessageHandlers;

namespace Libplanet.Net;

public sealed class Swarm : ServiceBase, IServiceProvider
{
    private readonly ISigner _signer;
    private readonly ConsensusReactor? _consensusReactor;
    private readonly TxFetcher _txFetcher;
    private readonly EvidenceFetcher _evidenceFetcher;
    private readonly AccessLimiter _transferEvidenceLimiter;
    private readonly ServiceCollection _services;
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
        PeerDiscovery = new PeerDiscovery(RoutingTable, Transport);
        _txFetcher = new TxFetcher(Blockchain, Transport, options.TimeoutOptions);
        _evidenceFetcher = new EvidenceFetcher(Blockchain, Transport, options.TimeoutOptions);
        BlockDemandDictionary = new BlockDemandDictionary(options.BlockDemandLifespan);
        _transferEvidenceLimiter = new(options.TaskRegulationOptions.MaxTransferTxsTaskCount);
        _consensusReactor = consensusOption is not null ? new ConsensusReactor(signer, Blockchain, consensusOption) : null;

        _services =
        [
            new BlockBroadcastTask(this),
            new TxBroadcastTask(this),
            new EvidenceBroadcastTask(this),
            new FillBlocksTask(this),
            new PollBlocksTask(this),
            // new ConsumeBlockCandidatesTask(this),
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

    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        var dialTimeout = Options.PreloadOptions.DialTimeout;
        var tipDeltaThreshold = Options.PreloadOptions.TipDeltaThreshold;

        var i = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var blockchainStates = await this.GetBlockchainStateAsync(dialTimeout, cancellationToken)
                .ToArrayAsync(cancellationToken);
            if (blockchainStates.Length == 0)
            {
                break;
            }

            var tip = Blockchain.Tip;
            var topmostTip = blockchainStates
                .Select(item => item.Tip)
                .Aggregate((s, n) => s.Height > n.Height ? s : n);
            if (topmostTip.Height - (i > 0 ? tipDeltaThreshold : 0) <= tip.Height)
            {
                break;
            }

            BlockBranches.Clear();
            await PullBlocksAsync(blockchainStates, cancellationToken);
            if (BlockBranches.TryGetValue(Blockchain.Tip.BlockHash, out var blockBranch))
            {
                await BlockCandidateProcessAsync(blockBranch, cancellationToken);
            }
            // await ConsumeBlockCandidates(cancellationToken: cancellationToken);
            i++;
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    internal async Task<(Peer, BlockHash[])> GetDemandBlockHashes(
        Block block, BlockchainState[] blockchainStates, CancellationToken cancellationToken)
    {
        var tranasport = Transport;
        var exceptionList = new List<Exception>();
        foreach (var blockchainState in blockchainStates)
        {
            if (!IsBlockNeeded(blockchainState.Tip))
            {
                continue;
            }

            try
            {
                var peer = blockchainState.Peer;
                var blockHashes = await tranasport.GetBlockHashesAsync(peer, block.BlockHash, cancellationToken);
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

        var peers = blockchainStates.Select(item => item.Peer).ToArray();
        throw new AggregateException(
            "Failed to fetch demand block hashes from peers: " +
            string.Join(", ", peers.Select(p => p.ToString())),
            exceptionList);
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        if (Options.PreloadOptions.Enabled)
        {
            await SyncAsync(cancellationToken);
        }

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
        BlockBranches.RemoveAll(_ => true);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        Transport.MessageHandlers.RemoveRange(_messageHandlers);
        _transferEvidenceLimiter.Dispose();

        _txFetcher.Dispose();
        _evidenceFetcher.Dispose();
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

    internal void BroadcastTxIds(Address except, IEnumerable<TxId> txIds)
    {
        var message = new TxIdMessage { Ids = [.. txIds] };
        BroadcastMessage(except, message);
    }

    internal bool IsBlockNeeded(BlockSummary blockSummary) => blockSummary.Height > Blockchain.Tip.Height;

    public void BroadcastEvidence(ImmutableArray<EvidenceBase> evidence) => BroadcastEvidence(default, evidence);

    private void BroadcastEvidence(Address except, ImmutableArray<EvidenceBase> evidence)
    {
        var evidenceIds = evidence.Select(evidence => evidence.Id).ToArray();
        var replyMessage = new EvidenceIdMessage { Ids = [.. evidenceIds] };
        BroadcastMessage(except, replyMessage);
    }

    public BlockDemandDictionary BlockDemandDictionary { get; private set; }

    public BlockBranchCollection BlockBranches { get; } = [];

    internal async Task PullBlocksAsync(TimeSpan timeout, int maximumPollPeers, CancellationToken cancellationToken)
    {
        if (maximumPollPeers <= 0)
        {
            return;
        }

        var blockchainStates = await this.GetBlockchainStateAsync(timeout, cancellationToken)
            .Take(maximumPollPeers)
            .ToArrayAsync(cancellationToken);
        await PullBlocksAsync(blockchainStates, cancellationToken);
    }

    private async Task<bool> PullBlocksAsync(
        BlockchainState[] blockchainStates, CancellationToken cancellationToken)
    {
        try
        {
            (var peer, var blockHashes) = await GetDemandBlockHashes(Blockchain.Tip, blockchainStates, cancellationToken);
            var blockPairs = await Transport.GetBlocksAsync(peer, blockHashes, cancellationToken).ToArrayAsync(cancellationToken);

            if (blockPairs.Length > 0)
            {
                var blockBranch = new BlockBranch
                {
                    Blocks = [.. blockPairs.Select(item => item.Item1)],
                    BlockCommits = [.. blockPairs.Select(item => item.Item2)],
                };
                BlockBranches.Add(Blockchain.Tip.BlockHash, blockBranch);
                return true;
            }
        }
        catch (Exception)
        {
            // logging
        }

        return false;
    }

    internal async Task<bool> BlockCandidateProcessAsync(
        BlockBranch blockBranch, CancellationToken cancellationToken)
    {
        try
        {
            await AppendBranchAsync(blockBranch, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async ValueTask AppendBranchAsync(BlockBranch blockBranch, CancellationToken cancellationToken)
    {
        var blockchain = Blockchain;
        var branchPoint = blockchain.Tip;
        var actualBranch = blockBranch.TakeAfter(branchPoint);

        for (var i = 0; i < actualBranch.Blocks.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            blockchain.Append(actualBranch.Blocks[i], actualBranch.BlockCommits[i]);
            await Task.Yield();
        }
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
