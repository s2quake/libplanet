using System.Threading;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.NetMQ;
using Libplanet.Data;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Serilog;
using xRetry;
using static Libplanet.Tests.TestUtils;
using Libplanet.TestUtilities;
using Libplanet.Tests;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Components;
using Libplanet.Net.Services;
using Libplanet.Extensions;
using Libplanet.Types.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Libplanet.Net.Tests;

public partial class SwarmTest
{
    [Fact(Timeout = Timeout)]
    public async Task BroadcastBlock()
    {
        const int blockCount = 5;
        using var fx = new MemoryRepositoryFixture();
        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };
        var peerExplorerA = new PeerExplorer(transportA);
        var peerExplorerB = new PeerExplorer(transportB);
        var blockchainA = TestUtils.CreateBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainB = TestUtils.CreateBlockchain(genesisBlock: fx.GenesisBlock);
        var serviceA = new BlockSynchronizationResponderService(blockchainA, transportA);
        var serviceB = new BlockSynchronizationService(blockchainB, transportB);
        await using var services = new ServiceCollection
        {
            serviceA,
            serviceB,
        };

        blockchainA.ProposeAndAppendMany(blockCount);

        Assert.Equal(blockCount, blockchainA.Tip.Height);
        Assert.NotEqual(blockchainA.Tip, blockchainB.Tip);
        Assert.NotNull(blockchainA.BlockCommits[blockchainA.Tip.BlockHash]);

        await transports.StartAsync(default);
        await services.StartAsync(default);

        await peerExplorerA.PingAsync(transportB.Peer, default);
        await peerExplorerB.PingAsync(peerExplorerA.Peer, default);

        var waitTaskB = serviceB.Synchronized.WaitAsync();
        peerExplorerA.Broadcast(blockchainA.Genesis.BlockHash, blockchainA.Tip);

        await waitTaskB;

        Assert.Equal(blockchainA.Tip, blockchainB.Tip);
        Assert.Equal(
            blockchainA.BlockCommits[blockchainA.Tip.BlockHash],
            blockchainB.BlockCommits[blockchainB.Tip.BlockHash]);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastBlockToReconnectedPeer()
    {
        using var fx = new MemoryRepositoryFixture();
        var minerKey = new PrivateKey();
        var privateKey = new PrivateKey();
        var blockchainOptions = fx.Options;
        var blockchain = MakeBlockchain(blockchainOptions);
        var seedBlockchain = MakeBlockchain(options: blockchainOptions, genesisBlock: blockchain.Genesis);
        var blockchainA = MakeBlockchain(options: blockchainOptions, genesisBlock: blockchain.Genesis);
        var blockchainB = MakeBlockchain(options: blockchainOptions, genesisBlock: blockchain.Genesis);

        var seedTransport = TestUtils.CreateTransport();
        var transportA = TestUtils.CreateTransport(privateKey);
        var transportB = TestUtils.CreateTransport(privateKey);
        await using var transports = new ServiceCollection
        {
            seedTransport,
            transportA,
            transportB,
        };
        using var seedPeerExplorer = new PeerExplorer(seedTransport);
        using var peerExplorerA = new PeerExplorer(transportA);
        using var peerExplorerB = new PeerExplorer(transportB);
        var seedServices = new ServiceCollection
        {
            new BlockBroadcastService(seedBlockchain, seedPeerExplorer),
            new BlockSynchronizationResponderService(seedBlockchain, seedTransport),
        };
        var serviceA = new BlockSynchronizationService(blockchainA, transportA);
        var serviceB = new BlockSynchronizationService(blockchainB, transportB);

        await using var services = new ServiceCollection
        {
            seedServices,
            serviceA,
            serviceB,
        };

        blockchain.ProposeAndAppendMany(minerKey, 10);
        blockchain.AppendTo(seedBlockchain, 1..5);

        await transports.StartAsync(default);
        await services.StartAsync(default);

        await peerExplorerA.PingAsync(seedTransport.Peer, default);
        await transportA.StopAsync(default);
        await seedPeerExplorer.RefreshAsync(TimeSpan.Zero, default);

        Assert.DoesNotContain(transportA.Peer, seedPeerExplorer.Peers);

        blockchain.AppendTo(seedBlockchain, 5..);

        await peerExplorerB.PingAsync(seedTransport.Peer, default);

        Assert.Contains(transportB.Peer, seedPeerExplorer.Peers);
        Assert.Contains(seedTransport.Peer, peerExplorerB.Peers);

        seedPeerExplorer.Broadcast(seedBlockchain.Genesis.BlockHash, seedBlockchain.Tip);
        await serviceB.Synchronized.WaitAsync();

        Assert.NotEqual(seedBlockchain.Blocks.Keys, blockchainA.Blocks.Keys);
        Assert.Equal(seedBlockchain.Blocks.Keys, blockchainB.Blocks.Keys);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastIgnoreFromDifferentGenesisHash()
    {
        var random = RandomUtility.GetRandom(output);
        using var fx = new MemoryRepositoryFixture();
        var privateKeyA = RandomUtility.PrivateKey(random);

        var transportA = TestUtils.CreateTransport(privateKeyA);
        var transportB = TestUtils.CreateTransport();
        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };
        var blockchainA = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainB = MakeBlockchain();
        using var peerExplorerA = new PeerExplorer(transportA);
        using var peerExplorerB = new PeerExplorer(transportB);

        var servicesA = new BlockSynchronizationResponderService(blockchainA, transportA);
        var servicesB = new BlockSynchronizationService(blockchainB, transportB);
        await using var services = new ServiceCollection
        {
            servicesA,
            servicesB,
        };

        await transports.StartAsync(default);
        await services.StartAsync(default);

        await peerExplorerB.PingAsync(transportA.Peer, default);

        blockchainA.ProposeAndAppend(privateKeyA);
        var waitTask = transportB.MessageRouter.ErrorOccurred.WaitAsync(
            e => e.MessageHandler is BlockSummaryMessageHandler,
            default);
        peerExplorerA.Broadcast(blockchainA.Genesis.BlockHash, blockchainA.Tip);
        var result = await waitTask;
        Assert.IsType<InvalidMessageException>(result.Exception);
        Assert.NotEqual(blockchainA.Tip, blockchainB.Tip);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastWhileMining()
    {
        var minerA = new PrivateKey();
        var minerB = new PrivateKey();
        var transportA = TestUtils.CreateTransport(minerA);
        var transportB = TestUtils.CreateTransport(minerB);
        using var peerExplorerA = new PeerExplorer(transportA);
        using var peerExplorerB = new PeerExplorer(transportB);
        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };
        var blockchainA = MakeBlockchain();
        var blockchainB = MakeBlockchain();
        var serviceA = new BlockSynchronizationResponderService(blockchainA, transportA);
        var broadcastServiceA = new BlockBroadcastService(blockchainA, peerExplorerA);
        var syncServiceB = new BlockSynchronizationService(blockchainB, transportB);
        await using var services = new ServiceCollection
        {
            serviceA,
            broadcastServiceA,
            syncServiceB,
        };

        await transports.StartAsync(default);
        await services.StartAsync(default);

        await peerExplorerA.PingAsync(transportB.Peer, default);

        await Task.Run(async () =>
        {
            while (blockchainA.Tip.Height < 10)
            {
                var interval = RandomUtility.TimeSpan(100, 1000);
                await Task.Delay(interval);
                blockchainA.ProposeAndAppend(minerA);
            }
        });

        using var cancellationSource = new CancellationTokenSource(2000);
        await syncServiceB.Synchronized.WaitAsync(e => e.Blocks[^1].Height == 10, cancellationSource.Token);

        Assert.Equal(blockchainA.Blocks.Keys, blockchainB.Blocks.Keys);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastTx()
    {
        using var fx = new MemoryRepositoryFixture();
        var privateKeyA = new PrivateKey();
        var transportA = TestUtils.CreateTransport(privateKeyA);
        var transportB = TestUtils.CreateTransport();
        var transportC = TestUtils.CreateTransport();
        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
        };
        var peerExplorerA = new PeerExplorer(transportA);
        var peerExplorerB = new PeerExplorer(transportB);
        var peerExplorerC = new PeerExplorer(transportC);

        var blockchainA = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainB = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainC = MakeBlockchain(genesisBlock: fx.GenesisBlock);

        var serviceA = new TransactionSynchronizationResponderService(blockchainA, transportA);
        var serviceB = new TransactionSynchronizationService(blockchainB, transportB);
        var serviceC = new TransactionSynchronizationService(blockchainC, transportC);
        await using var services = new ServiceCollection
        {
            serviceA,
            serviceB,
            serviceC
        };

        await transports.StartAsync(default);
        await services.StartAsync(default);

        await peerExplorerA.PingAsync(transportB.Peer, default);
        await peerExplorerB.PingAsync(transportC.Peer, default);
        await peerExplorerC.PingAsync(transportA.Peer, default);

        var txKey = new PrivateKey();
        var tx = new TransactionBuilder
        {
            GenesisHash = blockchainA.Genesis.BlockHash,
        }.Create(txKey);

        blockchainA.StagedTransactions.Add(tx);
        blockchainA.ProposeAndAppend(privateKeyA);

        var waitTaskB = serviceB.Synchronized.WaitAsync();
        var waitTaskC = serviceC.Synchronized.WaitAsync();
        peerExplorerA.Broadcast([tx.Id]);

        await waitTaskB.WaitAsync(TimeSpan.FromSeconds(3));
        await waitTaskC.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(tx, blockchainB.StagedTransactions[tx.Id]);
        Assert.Equal(tx, blockchainC.StagedTransactions[tx.Id]);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastTxWhileMining()
    {
        using var fx = new MemoryRepositoryFixture();
        var privateKeyC = new PrivateKey();
        var transportA = TestUtils.CreateTransport();
        var transportC = TestUtils.CreateTransport(privateKeyC);
        var peerExplorerA = new PeerExplorer(transportA);
        var peerExplorerC = new PeerExplorer(transportC);
        var blockchainA = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainC = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var serviceA = new TransactionSynchronizationResponderService(blockchainA, transportA);
        var serviceC = new TransactionSynchronizationService(blockchainC, transportC);
        await using var services = new ServiceCollection
        {
            transportA,
            transportC,
            serviceA,
            serviceC
        };

        var privateKey = new PrivateKey();
        var address = privateKey.Address;
        var txCount = 10;
        var txs = Enumerable.Range(0, txCount).Select(_ =>
                blockchainA.StagedTransactions.Add(new PrivateKey(), new TransactionSubmission
                {
                    Actions = [DumbAction.Create((address, "foo"))],
                }))
            .ToImmutableArray();

        await services.StartAsync(default);

        await peerExplorerC.PingAsync(transportA.Peer, default);
        Assert.Contains(transportC.Peer, peerExplorerA.Peers);
        Assert.Contains(transportA.Peer, peerExplorerC.Peers);

        var miningTask = Task.Run(async () =>
        {
            for (var i = 0; i < 10; i++)
            {
                blockchainC.ProposeAndAppend(privateKeyC);
                await Task.Delay(100);
            }
        });

        for (var i = 0; i < 100; i++)
        {
            peerExplorerA.Broadcast(txs);
        }

        var waitTaskC = serviceC.Synchronized.WaitAsync(
            _ => blockchainC.StagedTransactions.Count == txCount);
        await miningTask;
        await waitTaskC.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.All(txs, tx => Assert.Contains(tx.Id, blockchainC.Transactions.Keys));
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastTxAsync()
    {
        using var fx = new MemoryRepositoryFixture();
        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var transportC = TestUtils.CreateTransport();
        var peerExplorerA = new PeerExplorer(transportA);
        var peerExplorerB = new PeerExplorer(transportB);
        var peerExplorerC = new PeerExplorer(transportC);
        var blockchainA = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainB = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainC = MakeBlockchain(genesisBlock: fx.GenesisBlock);

        var syncServiceB = new TransactionSynchronizationService(blockchainB, transportB);
        var syncServiceC = new TransactionSynchronizationService(blockchainC, transportC);
        await using var services = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
            new TransactionSynchronizationResponderService(blockchainA, transportA),
            new TransactionBroadcastService(blockchainA, peerExplorerA),
            new TransactionSynchronizationResponderService(blockchainB, transportB),
            new TransactionBroadcastService(blockchainB, peerExplorerB),
            syncServiceB,
            syncServiceC,
        };

        var txKey = new PrivateKey();
        var tx = new TransactionBuilder
        {
            GenesisHash = blockchainA.Genesis.BlockHash,
            Actions = [],
        }.Create(txKey);

        blockchainA.StagedTransactions.Add(tx);

        await services.StartAsync(default);

        var waitTaskB = syncServiceB.Synchronized.WaitAsync();
        await peerExplorerA.PingAsync(peerExplorerB.Peer, default);

        await waitTaskB.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(tx, blockchainB.StagedTransactions[tx.Id]);

        await transportA.DisposeAsync();

        var waitTaskC = syncServiceC.Synchronized.WaitAsync();
        await peerExplorerB.PingAsync(peerExplorerC.Peer, default);

        await waitTaskC.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(tx, blockchainC.StagedTransactions[tx.Id]);
    }

    [RetryFact(Timeout = Timeout)]
    public async Task BroadcastTxAsyncMany()
    {
        const int size = 5;

        using var fx = new MemoryRepositoryFixture();
        var transports = new ITransport[size];
        var peerExplorers = new PeerExplorer[size];
        var blockchains = new Blockchain[size];
        var broadcastServices = new TransactionBroadcastService[size];
        var syncResponseServices = new TransactionSynchronizationResponderService[size];
        var syncServices = new TransactionSynchronizationService[size];

        await using var services = new ServiceCollection();
        for (var i = 0; i < size; i++)
        {
            transports[i] = TestUtils.CreateTransport();
            peerExplorers[i] = new PeerExplorer(transports[i]);
            blockchains[i] = MakeBlockchain(genesisBlock: fx.GenesisBlock);
            broadcastServices[i] = new TransactionBroadcastService(blockchains[i], peerExplorers[i]);
            syncResponseServices[i] = new TransactionSynchronizationResponderService(blockchains[i], transports[i]);
            syncServices[i] = new TransactionSynchronizationService(blockchains[i], transports[i]);

            services.Add(transports[i]);
            services.Add(broadcastServices[i]);
            services.Add(syncResponseServices[i]);
            services.Add(syncServices[i]);
        }

        var txKey = new PrivateKey();
        var tx = new TransactionBuilder
        {
            GenesisHash = blockchains[size - 1].Genesis.BlockHash,
            Actions = [],
        }.Create(txKey);
        blockchains[size - 1].StagedTransactions.Add(tx);

        await services.StartAsync(default);

        var waitTaskList = new List<Task>();
        for (var i = 0; i < size - 1; i++)
        {
            waitTaskList.Add(syncServices[i].Synchronized.WaitAsync());
        }

        for (var i = 1; i < size; i++)
        {
            await peerExplorers[i].PingAsync(peerExplorers[0].Peer, default);
        }

        await Task.WhenAll(waitTaskList);

        for (var i = 0; i < size; i++)
        {
            Assert.Equal(tx, blockchains[i].StagedTransactions[tx.Id]);
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotRebroadcastTxsWithLowerNonce()
    {
        // If the bucket stored peers are the same, the block may not propagate,
        // so specify private keys to make the buckets different.
        var keyA = PrivateKey.Parse("8568eb6f287afedece2c7b918471183db0451e1a61535bb0381cfdf95b85df20");
        var keyB = PrivateKey.Parse("c34f7498befcc39a14f03b37833f6c7bb78310f1243616524eda70e078b8313c");
        var keyC = PrivateKey.Parse("941bc2edfab840d79914d80fe3b30840628ac37a5d812d7f922b5d2405a223d3");
        using var fx = new MemoryRepositoryFixture();
        await using var transportA = TestUtils.CreateTransport(keyA);
        await using var transportB = TestUtils.CreateTransport(keyB);
        await using var transportC = TestUtils.CreateTransport(keyC);
        var peerExplorerA = new PeerExplorer(transportA);
        var peerExplorerB = new PeerExplorer(transportB);
        var peerExplorerC = new PeerExplorer(transportC);
        var blockchainA = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainB = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainC = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var syncServiceB = new TransactionSynchronizationService(blockchainB, transportB);
        var syncServiceC = new TransactionSynchronizationService(blockchainC, transportC);
        await using var services = new ServiceCollection
        {
            new TransactionSynchronizationResponderService(blockchainA, transportA),
            syncServiceB,
            syncServiceC,
            // transportA,
            // transportB,
            // transportC,
            // new TransactionSynchronizationResponderService(blockchainA, transportA),
            // new TransactionBroadcastService(blockchainA, peerExplorerA),
            // new TransactionSynchronizationResponderService(blockchainB, transportB),
            // new TransactionBroadcastService(blockchainB, peerExplorerB),
            // new TransactionSynchronizationResponderService(blockchainC, transportC),
            // new TransactionBroadcastService(blockchainC, peerExplorerC),
        };

        // var autoBroadcastDisabled = new SwarmOptions
        // {
        //     BlockBroadcastInterval = TimeSpan.FromSeconds(Timeout),
        //     TxBroadcastInterval = TimeSpan.FromSeconds(Timeout),
        // };

        // var swarmA =
        //     await CreateSwarm(keyA, options: autoBroadcastDisabled);
        // var swarmB =
        //     await CreateSwarm(keyB, options: autoBroadcastDisabled);
        // var swarmC =
        //     await CreateSwarm(keyC, options: autoBroadcastDisabled);

        // Blockchain chainA = swarmA.Blockchain;
        // Blockchain chainB = swarmB.Blockchain;
        // Blockchain chainC = swarmC.Blockchain;

        var privateKey = new PrivateKey();

        // var tx1 = swarmA.Blockchain.StagedTransactions.Add(privateKey);
        // var tx2 = swarmA.Blockchain.StagedTransactions.Add(privateKey);
        var tx1 = blockchainA.StagedTransactions.Add(privateKey);
        var tx2 = blockchainA.StagedTransactions.Add(privateKey);
        Assert.Equal(0, tx1.Nonce);
        Assert.Equal(1, tx2.Nonce);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await services.StartAsync(default);
        await peerExplorerA.PingAsync(peerExplorerB.Peer, default);
        var waitTaskB = syncServiceB.Synchronized.WaitAsync();
        peerExplorerA.Broadcast([tx1, tx2]);
        await waitTaskB.WaitAsync(TimeSpan.FromSeconds(5));
        // await swarmA.StartAsync(default);
        // await swarmB.StartAsync(default);
        // await swarmA.AddPeersAsync([swarmB.Peer], default);
        // swarmA.BroadcastTxs([tx1, tx2]);
        // await swarmB.TxReceived.WaitAsync(default);
        Assert.Equal(
            new HashSet<TxId> { tx1.Id, tx2.Id },
            [.. blockchainB.StagedTransactions.Keys]);
        peerExplorerA.Peers.Remove(peerExplorerB.Peer);
        peerExplorerB.Peers.Remove(peerExplorerA.Peer);
        // swarmA.PeerExplorer.Remove(swarmB.Peer);
        // swarmB.PeerExplorer.Remove(swarmA.Peer);

        blockchainA.StagedTransactions.Remove(tx2.Id);
        Assert.Equal(1, blockchainA.GetNextTxNonce(privateKey.Address));

        // await swarmA.StopAsync(default);
        // await swarmB.StopAsync(default);
        await transportA.StopAsync(default);
        await transportB.StopAsync(default);

        peerExplorerA.Peers.Remove(peerExplorerB.Peer);
        peerExplorerB.Peers.Remove(peerExplorerA.Peer);
        Assert.Empty(peerExplorerA.Peers);
        Assert.Empty(peerExplorerB.Peers);

        // swarmA.PeerExplorer.Remove(swarmB.Peer);
        // swarmB.PeerExplorer.Remove(swarmA.Peer);
        // Assert.Empty(swarmA.Peers);
        // Assert.Empty(swarmB.Peers);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        blockchainB.ProposeAndAppend(keyB);
        // Block block = chainB.ProposeBlock(keyB);
        // chainB.Append(block, TestUtils.CreateBlockCommit(block));

        var tx3 = blockchainA.StagedTransactions.Add(privateKey);
        var tx4 = blockchainA.StagedTransactions.Add(privateKey);
        Assert.Equal(1, tx3.Nonce);
        Assert.Equal(2, tx4.Nonce);

        // await swarmC.StartAsync(default);
        await transportC.StartAsync(default);
        await peerExplorerA.PingAsync(peerExplorerB.Peer, default);
        await peerExplorerB.PingAsync(peerExplorerA.Peer, default);
        await peerExplorerB.PingAsync(peerExplorerC.Peer, default);
        // await swarmA.AddPeersAsync([swarmB.Peer], default);
        // await swarmB.AddPeersAsync([swarmC.Peer], default);

        var waitTaskC = syncServiceC.Synchronized.WaitAsync();
        peerExplorerA.Broadcast([tx3, tx4]);
        await waitTaskC.WaitAsync(TimeSpan.FromSeconds(5));
        // await swarmB.TxReceived.WaitAsync(default);

        // SwarmB receives tx3 and is staged, but policy filters it.
        Assert.DoesNotContain(tx3.Id, blockchainB.StagedTransactions.Keys);
        Assert.Contains(tx3.Id, blockchainB.StagedTransactions.Keys);
        Assert.Contains(tx4.Id, blockchainB.StagedTransactions.Keys);

        // await swarmC.TxReceived.WaitAsync(default);
        // SwarmC can not receive tx3 because SwarmB does not rebroadcast it.
        Assert.DoesNotContain(tx3.Id, blockchainC.StagedTransactions.Keys);
        Assert.DoesNotContain(tx3.Id, blockchainC.StagedTransactions.Keys);
        Assert.Contains(tx4.Id, blockchainC.StagedTransactions.Keys);
    }

    [Fact(Timeout = Timeout)]
    public async Task CanBroadcastBlock()
    {
        // If the bucket stored peers are the same, the block may not propagate,
        // so specify private keys to make the buckets different.
        PrivateKey keyA = PrivateKey.Parse(
            "8568eb6f287afedece2c7b918471183db0451e1a61535bb0381cfdf95b85df20");
        PrivateKey keyB = PrivateKey.Parse(
            "c34f7498befcc39a14f03b37833f6c7bb78310f1243616524eda70e078b8313c");
        PrivateKey keyC = PrivateKey.Parse(
            "941bc2edfab840d79914d80fe3b30840628ac37a5d812d7f922b5d2405a223d3");

        var swarmA = await CreateSwarm(keyA);
        var swarmB = await CreateSwarm(keyB);
        var swarmC = await CreateSwarm(keyC);

        Blockchain chainA = swarmA.Blockchain;
        Blockchain chainB = swarmB.Blockchain;
        Blockchain chainC = swarmC.Blockchain;

        foreach (int i in Enumerable.Range(0, 10))
        {
            Block block = chainA.ProposeBlock(keyA);
            chainA.Append(block, TestUtils.CreateBlockCommit(block));
            if (i < 5)
            {
                chainB.Append(block, TestUtils.CreateBlockCommit(block));
            }
        }

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);
        await swarmC.StartAsync(default);

        await swarmB.AddPeersAsync([swarmA.Peer], default);
        await swarmC.AddPeersAsync([swarmA.Peer], default);

        swarmB.BroadcastBlock(chainB.Tip);

        // chainA ignores block header received because its index is shorter.
        // await swarmA.BlockHeaderReceived.WaitAsync(default);
        // await swarmC.BlockAppended.WaitAsync(default);
        // Assert.False(swarmA.BlockAppended.IsSet);

        // chainB doesn't applied to chainA since chainB is shorter
        // than chainA
        Assert.NotEqual(chainB, chainA);

        swarmA.BroadcastBlock(chainA.Tip);

        // await swarmB.BlockAppended.WaitAsync(default);
        // await swarmC.BlockAppended.WaitAsync(default);

        Log.Debug("Compare chainA and chainB");
        Assert.Equal(chainA.Blocks.Keys, chainB.Blocks.Keys);
        Log.Debug("Compare chainA and chainC");
        Assert.Equal(chainA.Blocks.Keys, chainC.Blocks.Keys);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastBlockWithSkip()
    {
        var options = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
        };
        var fx1 = new MemoryRepositoryFixture(options);
        var blockChain = MakeBlockchain(options);
        var privateKey = new PrivateKey();
        var minerSwarm = await CreateSwarm(blockChain, privateKey);
        var fx2 = new MemoryRepositoryFixture();
        // var receiverRenderer = new RecordingActionRenderer();
        // var loggedRenderer = new LoggedActionRenderer(
        //     receiverRenderer,
        //     _logger);
        var receiverChain = MakeBlockchain(options);
        Swarm receiverSwarm = await CreateSwarm(receiverChain);

        int renderCount = 0;

        // receiverRenderer.RenderEventHandler += (_, a) => renderCount += IsDumbAction(a) ? 1 : 0;

        Transaction[] transactions =
        [
            fx1.MakeTransaction(
                [
                    DumbAction.Create((fx1.Address2, "foo")),
                    DumbAction.Create((fx1.Address2, "bar")),
                ],
                timestamp: DateTimeOffset.MinValue,
                nonce: 0,
                privateKey: privateKey),
            fx1.MakeTransaction(
                [
                    DumbAction.Create((fx1.Address2, "baz")),
                    DumbAction.Create((fx1.Address2, "qux")),
                ],
                timestamp: DateTimeOffset.MinValue.AddSeconds(5),
                nonce: 1,
                privateKey: privateKey),
        ];

        Block block1 = blockChain.ProposeBlock(GenesisProposer);
        blockChain.Append(block1, TestUtils.CreateBlockCommit(block1));
        Block block2 = blockChain.ProposeBlock(GenesisProposer);
        blockChain.Append(block2, TestUtils.CreateBlockCommit(block2));

        await minerSwarm.StartAsync(default);
        await receiverSwarm.StartAsync(default);

        await receiverSwarm.AddPeersAsync([minerSwarm.Peer], default);

        minerSwarm.BroadcastBlock(block2);

        await AssertThatEventually(
            () => receiverChain.Tip.Equals(block2),
            5_000,
            1_000);
        Assert.Equal(3, receiverChain.Blocks.Count);
        Assert.Equal(4, renderCount);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastBlockWithoutGenesis()
    {
        var keyA = new PrivateKey();
        var keyB = new PrivateKey();

        Swarm swarmA = await CreateSwarm(keyA);
        Swarm swarmB = await CreateSwarm(keyB);

        Blockchain chainA = swarmA.Blockchain;
        Blockchain chainB = swarmB.Blockchain;

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);

        await swarmB.AddPeersAsync([swarmA.Peer], default);
        var block = chainA.ProposeBlock(keyA);
        chainA.Append(block, TestUtils.CreateBlockCommit(block));
        swarmA.BroadcastBlock(chainA.Blocks[-1]);

        // await swarmB.BlockAppended.WaitAsync(default);

        Assert.Equal(chainB.Blocks.Keys, chainA.Blocks.Keys);

        block = chainA.ProposeBlock(keyB);
        chainA.Append(block, TestUtils.CreateBlockCommit(block));
        swarmA.BroadcastBlock(chainA.Blocks[-1]);

        // await swarmB.BlockAppended.WaitAsync(default);

        Assert.Equal(chainB.Blocks.Keys, chainA.Blocks.Keys);
    }

    [Fact(Timeout = Timeout)]
    public async Task IgnoreExistingBlocks()
    {
        var keyA = new PrivateKey();
        var keyB = new PrivateKey();

        Swarm swarmA = await CreateSwarm(keyA);
        Swarm swarmB =
            await CreateSwarm(keyB, genesis: swarmA.Blockchain.Genesis);

        Blockchain chainA = swarmA.Blockchain;
        Blockchain chainB = swarmB.Blockchain;

        var block = chainA.ProposeBlock(keyA);
        BlockCommit blockCommit = TestUtils.CreateBlockCommit(block);
        chainA.Append(block, blockCommit);
        chainB.Append(block, blockCommit);

        foreach (int i in Enumerable.Range(0, 3))
        {
            block = chainA.ProposeBlock(keyA);
            chainA.Append(block, TestUtils.CreateBlockCommit(block));
        }

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);

        await swarmB.AddPeersAsync([swarmA.Peer], default);
        swarmA.BroadcastBlock(chainA.Blocks[-1]);
        // await swarmB.BlockAppended.WaitAsync(default);

        Assert.Equal(chainA.Blocks.Keys, chainB.Blocks.Keys);

        CancellationTokenSource cts = new CancellationTokenSource();
        swarmA.BroadcastBlock(chainA.Blocks[-1]);
        // Task t = swarmB.BlockAppended.WaitAsync(cts.Token);

        // Actually, previous code may pass this test if message is
        // delayed over 5 seconds.
        await Task.Delay(5000);
        // Assert.False(t.IsCompleted);

        cts.Cancel();
    }

    [Fact(Timeout = Timeout)]
    public async Task PullBlocks()
    {
        var keyA = new PrivateKey();
        var keyB = new PrivateKey();
        var keyC = new PrivateKey();
        using var fx = new MemoryRepositoryFixture();

        await using var transportA = TestUtils.CreateTransport(keyA);
        await using var transportB = TestUtils.CreateTransport(keyB);
        await using var transportC = TestUtils.CreateTransport(keyC);
        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
        };

        using var peerExplorerA = new PeerExplorer(transportA);
        using var peerExplorerB = new PeerExplorer(transportB);
        using var peerExplorerC = new PeerExplorer(transportC);

        var blockchainA = TestUtils.CreateBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainB = TestUtils.CreateBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainC = TestUtils.CreateBlockchain(genesisBlock: fx.GenesisBlock);

        transportA.MessageRouter.Register(new PingMessageHandler(transportA));
        transportA.MessageRouter.Register(new BlockchainStateRequestMessageHandler(blockchainA, transportA));
        transportA.MessageRouter.Register(new BlockHashRequestMessageHandler(blockchainA, transportA));
        transportA.MessageRouter.Register(new BlockRequestMessageHandler(blockchainA, transportA, 1));

        transportB.MessageRouter.Register(new PingMessageHandler(transportB));
        transportB.MessageRouter.Register(new BlockHashRequestMessageHandler(blockchainB, transportB));
        transportB.MessageRouter.Register(new BlockRequestMessageHandler(blockchainB, transportB, 1));
        transportB.MessageRouter.Register(new BlockchainStateRequestMessageHandler(blockchainB, transportB));

        transportC.MessageRouter.Register(new PingMessageHandler(transportC));

        blockchainA.ProposeAndAppendMany(keyA, 5);
        blockchainA.AppendTo(blockchainB, new Range(1, 3));

        var tipA = blockchainA.Tip;

        for (var i = 0; i < 10; i++)
        {
            var block = blockchainB.ProposeBlock(keyB);
            blockchainB.Append(block, TestUtils.CreateBlockCommit(block));
        }

        await transports.StartAsync(default);

        await peerExplorerB.PingAsync(transportA.Peer, default);
        await peerExplorerC.PingAsync(transportA.Peer, default);
        await peerExplorerB.ExploreAsync([transportA.Peer], 3, default);
        await peerExplorerC.ExploreAsync([transportA.Peer], 3, default);

        var blockDemandCollector = new BlockDemandCollector(blockchainC, transportC);
        using var blockFetcher = new BlockFetcher(blockchainC, transportC);
        using var blockBranchResolver = new BlockBranchResolver(blockchainC, blockFetcher);
        var blockBranchAppender = new BlockBranchAppender(blockchainC);

        await blockDemandCollector.ExecuteAsync([.. peerExplorerC.Peers], default);
        await blockBranchResolver.ResolveAsync(blockDemandCollector.BlockDemands, blockchainC.Tip, default);
        await blockBranchAppender.AppendAsync(blockBranchResolver.BlockBranches, default);

        Assert.Equal(blockchainC.Tip, tipA);
    }

    [Fact(Timeout = Timeout)]
    public async Task CanFillWithInvalidTransaction()
    {
        var privateKey = new PrivateKey();
        var address = privateKey.Address;
        var swarm1 = await CreateSwarm();
        var swarm2 = await CreateSwarm();

        var tx1 = swarm2.Blockchain.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = [DumbAction.Create((address, "foo"))],
        });

        var tx2 = swarm2.Blockchain.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = [DumbAction.Create((address, "bar"))],
        });

        var tx3 = swarm2.Blockchain.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = [DumbAction.Create((address, "quz"))],
        });

        var tx4 = new TransactionMetadata
        {
            Nonce = 4,
            Signer = privateKey.Address,
            GenesisHash = swarm1.Blockchain.Genesis.BlockHash,
            Actions = new[] { DumbAction.Create((address, "qux")) }.ToBytecodes(),
        }.Sign(privateKey);

        await swarm1.StartAsync(default);
        await swarm2.StartAsync(default);
        await swarm1.AddPeersAsync([swarm2.Peer], default);

        var transport = swarm1.Transport;
        var msg = new TransactionRequestMessage { TxIds = [tx1.Id, tx2.Id, tx3.Id, tx4.Id] };
        // var reply = await transport.SendAsync(swarm2.Peer, msg, default);
        // var replayMessage = (AggregateMessage)reply.Message;

        // Assert.Equal(3, replayMessage.Messages.Length);
        // Assert.Equal(
        //     new[] { tx1, tx2, tx3 }.ToHashSet(),
        //     replayMessage.Messages.Select(
        //         m => ModelSerializer.DeserializeFromBytes<Transaction>(
        //             ((TransactionMessage)m).Payload.AsSpan())).ToHashSet());
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotSpawnMultipleTaskForSinglePeer()
    {
        var key = new PrivateKey();
        var transportOptions = new TransportOptions();
        Swarm receiver =
            await CreateSwarm();
        var mockTransport = new NetMQTransport(new PrivateKey().AsSigner(), transportOptions);
        int requestCount = 0;

        void MessageHandler(IMessage message, MessageEnvelope messageEnvelope)
        {
            switch (message)
            {
                case PingMessage ping:
                    mockTransport.Post(messageEnvelope.Sender, new PongMessage(), messageEnvelope.Identity);
                    break;

                case BlockHashRequestMessage gbhm:
                    requestCount++;
                    break;
            }
        }

        mockTransport.MessageRouter.Register<IMessage>(MessageHandler);

        Block block1 = ProposeNextBlock(
            receiver.Blockchain.Genesis,
            key,
            []);
        Block block2 = ProposeNextBlock(
            block1,
            key,
            []);

        await receiver.StartAsync(default);
        await mockTransport.StartAsync(default);

        // Send block header for block 1.
        var blockHeaderMsg1 = new BlockSummaryMessage
        {
            GenesisHash = receiver.Blockchain.Genesis.BlockHash,
            BlockSummary = block1
        };
        mockTransport.Post(receiver.Peer, blockHeaderMsg1);
        // await receiver.BlockHeaderReceived.WaitAsync(default);

        // Wait until FillBlockAsync task has spawned block demand task.
        await Task.Delay(1000);

        // Re-send block header for block 1, make sure it does not spawn new task.
        mockTransport.Post(
            receiver.Peer,
            blockHeaderMsg1);
        // await receiver.BlockHeaderReceived.WaitAsync(default);
        await Task.Delay(1000);

        // Send block header for block 2, make sure it does not spawn new task.
        var blockHeaderMsg2 = new BlockSummaryMessage
        {
            GenesisHash = receiver.Blockchain.Genesis.BlockHash,
            BlockSummary = block2
        };
        mockTransport.Post(receiver.Peer, blockHeaderMsg2);
        // await receiver.BlockHeaderReceived.WaitAsync(default);
        await Task.Delay(1000);

        Assert.Equal(1, requestCount);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastEvidence()
    {
        using var cancellationTokenSource = new CancellationTokenSource(Timeout);
        var minerA = new PrivateKey();
        var validatorAddress = new PrivateKey().Address;
        var swarmA = await CreateSwarm(minerA);
        var swarmB = await CreateSwarm();
        var swarmC = await CreateSwarm();

        var chainA = swarmA.Blockchain;
        var chainB = swarmB.Blockchain;
        var chainC = swarmC.Blockchain;

        var evidence = TestEvidence.Create(0, validatorAddress, DateTimeOffset.UtcNow);
        chainA.PendingEvidences.Add(evidence);

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);
        await swarmC.StartAsync(default);

        await swarmA.AddPeersAsync([swarmB.Peer], default);
        await swarmB.AddPeersAsync([swarmC.Peer], default);
        await swarmC.AddPeersAsync([swarmA.Peer], default);

        swarmA.BroadcastEvidence(default, [evidence]);

        // await swarmC.EvidenceReceived.WaitAsync(cancellationTokenSource.Token);
        // await swarmB.EvidenceReceived.WaitAsync(cancellationTokenSource.Token);

        Assert.Equal(evidence, chainB.PendingEvidences[evidence.Id]);
        Assert.Equal(evidence, chainC.PendingEvidences[evidence.Id]);
    }
}
