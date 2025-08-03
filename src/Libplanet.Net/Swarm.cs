using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;
using Libplanet.Net.NetMQ;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Components;

namespace Libplanet.Net;

public sealed class Swarm : ServiceBase, IServiceProvider
{
    private readonly ISigner _signer;
    private readonly ConsensusService? _consensusSerevice;
    private readonly ServiceCollection _services;
    private readonly IDisposable _handlerRegistration;
    private readonly PeerCollection _peers;

    public Swarm(
        ISigner signer,
        Blockchain blockchain,
        SwarmOptions options,
        ConsensusServiceOptions? consensusOption = null)
    {
        _signer = signer;
        Blockchain = blockchain;
        Options = options;
        Transport = new NetMQTransport(signer, options.TransportOptions);
        _peers = new PeerCollection(Transport.Peer.Address);
        PeerExplorer = new PeerExplorer(Transport, _peers);
        BlockDemands = new BlockDemandCollection();
        BlockBranches = new BlockBranchCollection();
        // _consensusSerevice = consensusOption is not null ? new ConsensusService(signer, Blockchain, consensusOption) : null;

        _services =
        [
            // new BlockBroadcastTask(this),
            // new TxBroadcastTask(this),
            // new EvidenceBroadcastTask(this),
            // new BlockBranchPollService(this),
            // new BlockDemandPollTask(this),
            // new ConsumeBlockCandidatesTask(this),
            // new RefreshTableTask(PeerExplorer, options.RefreshPeriod, options.RefreshLifespan),
            // new RebuildConnectionTask(this),
            // new RefreshStaticPeersService(this),
            // new TransactionFetcher(Blockchain, Transport, options.TimeoutOptions),
            // new EvidenceFetcher(Blockchain, Transport, options.TimeoutOptions),
        ];
        _handlerRegistration = Transport.MessageRouter.RegisterMany(
        [
            // new BlockRequestMessageHandler(this, options),
            // new BlockHashRequestMessageHandler(this),
            // new TransactionRequestMessageHandler(this, options),
            // new BlockchainStateRequestMessageHandler(this),
            // new BlockSummaryMessageHandler(this),
        ]);
    }

    public bool ConsensusRunning => _consensusSerevice?.IsRunning ?? false;

    public Address Address => _signer.Address;

    public Peer Peer => Transport.Peer;

    public IEnumerable<Peer> Peers => PeerExplorer.Peers;

    public ImmutableArray<Peer> Validators => _consensusSerevice?.Validators ?? [];

    public Blockchain Blockchain { get; private set; }

    internal PeerExplorer PeerExplorer { get; }

    internal ITransport Transport { get; }

    internal int FindNextHashesChunkSize { get; set; } = 500;

    internal SwarmOptions Options { get; }

    internal ConsensusService ConsensusService
        => _consensusSerevice ?? throw new InvalidOperationException("ConsensusService is not initialized.");

    public void BroadcastBlock(Block block)
    {
        BroadcastBlock(default, block);
    }

    public void BroadcastTxs(ImmutableArray<Transaction> txs)
    {
        BroadcastTxs(default, txs);
    }

    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        // var dialTimeout = Options.PreloadOptions.DialTimeout;
        // var tipDeltaThreshold = Options.PreloadOptions.TipDeltaThreshold;

        // var i = 0;
        // while (!cancellationToken.IsCancellationRequested)
        // {
        //     var blockchainStates = await this.GetBlockchainStateAsync(dialTimeout, cancellationToken)
        //         .ToArrayAsync(cancellationToken);
        //     if (blockchainStates.Length == 0)
        //     {
        //         break;
        //     }

        //     var tip = Blockchain.Tip;
        //     var topmostTip = blockchainStates
        //         .Select(item => item.Tip)
        //         .Aggregate((s, n) => s.Height > n.Height ? s : n);
        //     if (topmostTip.Height - (i > 0 ? tipDeltaThreshold : 0) <= tip.Height)
        //     {
        //         break;
        //     }

        //     BlockBranches.Clear();
        //     await PullBlocksAsync(blockchainStates, cancellationToken);
        //     if (BlockBranches.TryGetValue(Blockchain.Tip.BlockHash, out var blockBranch))
        //     {
        //         await BlockCandidateProcessAsync(blockBranch, cancellationToken);
        //     }
        //     // await ConsumeBlockCandidates(cancellationToken: cancellationToken);
        //     i++;
        // }

        // cancellationToken.ThrowIfCancellationRequested();
    }

    // internal async Task<(Peer, BlockHash[])> GetDemandBlockHashes(
    //     Block block, BlockchainState[] blockchainStates, CancellationToken cancellationToken)
    // {
    //     var tranasport = Transport;
    //     var exceptionList = new List<Exception>();
    //     foreach (var blockchainState in blockchainStates)
    //     {
    //         if (!IsBlockNeeded(blockchainState.Tip))
    //         {
    //             continue;
    //         }

    //         try
    //         {
    //             var peer = blockchainState.Peer;
    //             var blockHashes = await tranasport.GetBlockHashesAsync(peer, block.BlockHash, cancellationToken);
    //             if (blockHashes.Length != 0)
    //             {
    //                 return (peer, blockHashes);
    //             }
    //             else
    //             {
    //                 continue;
    //             }
    //         }
    //         catch (Exception e)
    //         {
    //             exceptionList.Add(e);
    //         }
    //     }

    //     var peers = blockchainStates.Select(item => item.Peer).ToArray();
    //     throw new AggregateException(
    //         "Failed to fetch demand block hashes from peers: " +
    //         string.Join(", ", peers.Select(p => p.ToString())),
    //         exceptionList);
    // }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        if (Options.PreloadOptions.Enabled)
        {
            await SyncAsync(cancellationToken);
        }

        await Transport.StartAsync(cancellationToken);
        // await PeerDiscovery.StartAsync(cancellationToken);
        if (_consensusSerevice is not null)
        {
            await _consensusSerevice.StartAsync(cancellationToken);
        }

        await _services.StartAsync(cancellationToken);
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        await _services.StopAsync(cancellationToken);
        await Transport.StopAsync(cancellationToken);
        if (_consensusSerevice is not null)
        {
            await _consensusSerevice.StopAsync(cancellationToken);
        }

        BlockDemands.Clear();
        BlockBranches.Clear();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _handlerRegistration.Dispose();

        await _services.DisposeAsync();
        await Transport.DisposeAsync();
        if (_consensusSerevice is not null)
        {
            await _consensusSerevice.DisposeAsync();
        }

        await base.DisposeAsyncCore();
    }

    private void BroadcastBlock(ImmutableArray<Peer> except, Block block)
    {
        var message = new BlockSummaryMessage
        {
            GenesisHash = Blockchain.Genesis.BlockHash,
            BlockSummary = block,
        };
        BroadcastMessage(except, message);
    }

    private void BroadcastTxs(ImmutableArray<Peer> except, ImmutableArray<Transaction> txs)
    {
        var txIds = txs.Select(tx => tx.Id).ToImmutableArray();
        BroadcastTxIds(except, txIds);
    }

    internal void BroadcastMessage(ImmutableArray<Peer> except, MessageBase message)
        => PeerExplorer.Broadcast(message, new BroadcastOptions { Except = except });

    internal void BroadcastTxIds(ImmutableArray<Peer> except, ImmutableArray<TxId> txIds)
    {
        var message = new TxIdMessage { Ids = [.. txIds] };
        BroadcastMessage(except, message);
    }

    internal bool IsBlockNeeded(BlockSummary blockSummary) => blockSummary.Height > Blockchain.Tip.Height;

    internal void BroadcastEvidence(ImmutableArray<Peer> except, ImmutableArray<EvidenceBase> evidence)
    {
        var evidenceIds = evidence.Select(evidence => evidence.Id).ToArray();
        var message = new EvidenceIdMessage { Ids = [.. evidenceIds] };
        BroadcastMessage(except, message);
    }

    public BlockDemandCollection BlockDemands { get; }

    public BlockBranchCollection BlockBranches { get; }

    // internal async Task PullBlocksAsync(TimeSpan timeout, int maximumPollPeers, CancellationToken cancellationToken)
    // {
    //     if (maximumPollPeers <= 0)
    //     {
    //         return;
    //     }

    //     var blockchainStates = await this.GetBlockchainStateAsync(timeout, cancellationToken)
    //         .Take(maximumPollPeers)
    //         .ToArrayAsync(cancellationToken);
    //     await PullBlocksAsync(blockchainStates, cancellationToken);
    // }

    // private async Task<bool> PullBlocksAsync(
    //     BlockchainState[] blockchainStates, CancellationToken cancellationToken)
    // {
    //     try
    //     {
    //         (var peer, var blockHashes) = await GetDemandBlockHashes(Blockchain.Tip, blockchainStates, cancellationToken);
    //         var blockPairs = await Transport.GetBlocksAsync(peer, blockHashes, cancellationToken).ToArrayAsync(cancellationToken);

    //         if (blockPairs.Length > 0)
    //         {
    //             var blockBranch = new BlockBranch
    //             {
    //                 Blocks = [.. blockPairs.Select(item => item.Item1)],
    //                 BlockCommits = [.. blockPairs.Select(item => item.Item2)],
    //             };
    //             BlockBranches.Add(Blockchain.Tip.BlockHash, blockBranch);
    //             return true;
    //         }
    //     }
    //     catch (Exception)
    //     {
    //         // logging
    //     }

    //     return false;
    // }

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
        // var blockchain = Blockchain;
        // var branchPoint = blockchain.Tip;
        // var actualBranch = blockBranch.TakeAfter(branchPoint);

        // for (var i = 0; i < actualBranch.Blocks.Length; i++)
        // {
        //     cancellationToken.ThrowIfCancellationRequested();
        //     blockchain.Append(actualBranch.Blocks[i], actualBranch.BlockCommits[i]);
        //     await Task.Yield();
        // }
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(ITransport))
        {
            return Transport;
        }

        if (serviceType == typeof(ConsensusService))
        {
            return _consensusSerevice;
        }

        if (serviceType == typeof(PeerExplorer))
        {
            return PeerExplorer;
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
