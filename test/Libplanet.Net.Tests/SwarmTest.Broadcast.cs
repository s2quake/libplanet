using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Messages;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Tests.Store;
using Libplanet.Types;
using static Libplanet.Tests.TestUtils;
using Libplanet.TestUtilities;
using Libplanet.Tests;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Components;
using Libplanet.Net.Services;
using Libplanet.Extensions;
using System.Reactive.Linq;

namespace Libplanet.Net.Tests;

public partial class SwarmTest
{
    [Fact(Timeout = Timeout)]
    public async Task BroadcastBlock()
    {
        const int blockCount = 5;
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fx = new MemoryRepositoryFixture();
        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var blockchainA = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainB = MakeBlockchain(genesisBlock: fx.GenesisBlock);
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

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        await peerExplorerA.PingAsync(transportB.Peer, cancellationToken);
        await peerExplorerB.PingAsync(peerExplorerA.Peer, cancellationToken);

        var waitTaskB = serviceB.Appended.WaitAsync();
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

        var transport = TestUtils.CreateTransport();
        var transportA = TestUtils.CreateTransport(privateKey);
        var transportB = TestUtils.CreateTransport(privateKey);
        var peers = new PeerCollection(transport.Peer.Address);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);

        using var peerExplorer = new PeerExplorer(transport, peers);
        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        var seedServices = new ServiceCollection
        {
            new BlockBroadcastService(seedBlockchain, peerExplorer),
            new BlockSynchronizationResponderService(seedBlockchain, transport),
        };
        var serviceA = new BlockSynchronizationService(blockchainA, transportA);
        var serviceB = new BlockSynchronizationService(blockchainB, transportB);

        await using var transports = new ServiceCollection
        {
            transport,
            transportA,
            transportB,
        };
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

        await peerExplorerA.PingAsync(transport.Peer, default);
        await transportA.StopAsync(default);
        await peerExplorer.RefreshAsync(TimeSpan.Zero, default);

        Assert.DoesNotContain(transportA.Peer, peerExplorer.Peers);

        blockchain.AppendTo(seedBlockchain, 5..);

        await peerExplorerB.PingAsync(transport.Peer, default);

        Assert.Contains(transportB.Peer, peerExplorer.Peers);
        Assert.Contains(transport.Peer, peerExplorerB.Peers);

        peerExplorer.Broadcast(seedBlockchain.Genesis.BlockHash, seedBlockchain.Tip);
        await serviceB.Appended.WaitAsync();

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
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);

        var blockchainA = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainB = MakeBlockchain();
        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);

        var servicesA = new BlockSynchronizationResponderService(blockchainA, transportA);
        var servicesB = new BlockSynchronizationService(blockchainB, transportB);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };
        await using var services = new ServiceCollection
        {
            servicesA,
            servicesB,
        };

        await transports.StartAsync(default);
        await services.StartAsync(default);

        await peerExplorerB.PingAsync(transportA.Peer, default);

        blockchainA.ProposeAndAppend(privateKeyA);
        var waitTask = transportB.MessageRouter.MessageHandlingFailed.WaitAsync(
            predicate: e => e.Handler is BlockSummaryMessageHandler);
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
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        var blockchainA = MakeBlockchain();
        var blockchainB = MakeBlockchain();
        var serviceA = new BlockSynchronizationResponderService(blockchainA, transportA);
        var broadcastServiceA = new BlockBroadcastService(blockchainA, peerExplorerA);
        var syncServiceB = new BlockSynchronizationService(blockchainB, transportB);
        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };
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
        await syncServiceB.Appended.WaitAsync(e => e.Blocks[^1].Height == 10, cancellationSource.Token);

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
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var peerExplorerC = new PeerExplorer(transportC, peersC);

        var blockchainA = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainB = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainC = MakeBlockchain(genesisBlock: fx.GenesisBlock);

        var serviceA = new TransactionSynchronizationResponderService(blockchainA, transportA);
        var serviceB = new TransactionSynchronizationService(blockchainB, transportB);
        var serviceC = new TransactionSynchronizationService(blockchainC, transportC);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
        };
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

        var waitTaskB = serviceB.Staged.WaitAsync();
        var waitTaskC = serviceC.Staged.WaitAsync();
        peerExplorerA.BroadcastTransaction([tx.Id]);

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
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerC = new PeerExplorer(transportC, peersC);
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
            peerExplorerA.BroadcastTransaction(txs);
        }

        var waitTaskC = serviceC.Staged.WaitAsync(
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
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var peerExplorerC = new PeerExplorer(transportC, peersC);
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

        var waitTaskB = syncServiceB.Staged.WaitAsync();
        await peerExplorerA.PingAsync(peerExplorerB.Peer, default);

        await waitTaskB.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(tx, blockchainB.StagedTransactions[tx.Id]);

        await transportA.DisposeAsync();

        var waitTaskC = syncServiceC.Staged.WaitAsync();
        await peerExplorerB.PingAsync(peerExplorerC.Peer, default);

        await waitTaskC.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(tx, blockchainC.StagedTransactions[tx.Id]);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastTxAsyncMany()
    {
        const int size = 5;

        using var fx = new MemoryRepositoryFixture();
        var transports = new ITransport[size];
        var peerses = new PeerCollection[size];
        var peerExplorers = new PeerExplorer[size];
        var blockchains = new Blockchain[size];
        var broadcastServices = new TransactionBroadcastService[size];
        var syncResponseServices = new TransactionSynchronizationResponderService[size];
        var syncServices = new TransactionSynchronizationService[size];

        await using var services = new ServiceCollection();
        for (var i = 0; i < size; i++)
        {
            transports[i] = TestUtils.CreateTransport();
            peerses[i] = new PeerCollection(transports[i].Peer.Address);
            peerExplorers[i] = new PeerExplorer(transports[i], peerses[i]);
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
            waitTaskList.Add(syncServices[i].Staged.WaitAsync());
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
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var peerExplorerC = new PeerExplorer(transportC, peersC);
        var blockchainA = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainB = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainC = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var syncServiceB = new TransactionSynchronizationService(blockchainB, transportB);
        var syncServiceC = new TransactionSynchronizationService(blockchainC, transportC);
        var broadcastServiceA = new TransactionBroadcastService(blockchainA, peerExplorerA);
        var broadcastServiceB = new TransactionBroadcastService(blockchainB, peerExplorerB);
        await using var services = new ServiceCollection
        {
            new TransactionSynchronizationResponderService(blockchainA, transportA),
            new TransactionSynchronizationResponderService(blockchainB, transportB),
            syncServiceB,
            syncServiceC,
            broadcastServiceA,
            broadcastServiceB,
        };

        var privateKey = new PrivateKey();
        var tx1 = blockchainA.StagedTransactions.Add(privateKey);
        var tx2 = blockchainA.StagedTransactions.Add(privateKey);
        Assert.Equal(0, tx1.Nonce);
        Assert.Equal(1, tx2.Nonce);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await services.StartAsync(default);
        await peerExplorerA.PingAsync(peerExplorerB.Peer, default);
        TestUtils.InvokeDelay(() => peerExplorerA.BroadcastTransaction([tx1, tx2]), 100);
        await syncServiceB.Staged.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(
            new HashSet<TxId> { tx1.Id, tx2.Id },
            [.. blockchainB.StagedTransactions.Keys]);
        peerExplorerA.Peers.Remove(peerExplorerB.Peer);
        peerExplorerB.Peers.Remove(peerExplorerA.Peer);

        Assert.Equal(2, blockchainA.GetNextTxNonce(privateKey.Address));
        blockchainA.StagedTransactions.Remove(tx2.Id);
        Assert.Equal(1, blockchainA.GetNextTxNonce(privateKey.Address));

        await transportA.StopAsync(default);
        await transportB.StopAsync(default);

        peerExplorerA.Peers.Remove(peerExplorerB.Peer);
        peerExplorerB.Peers.Remove(peerExplorerA.Peer);
        Assert.Empty(peerExplorerA.Peers);
        Assert.Empty(peerExplorerB.Peers);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        blockchainB.ProposeAndAppend(keyB);

        var tx3 = blockchainA.StagedTransactions.Add(privateKey);
        var tx4 = blockchainA.StagedTransactions.Add(privateKey);
        Assert.Equal(1, tx3.Nonce);
        Assert.Equal(2, tx4.Nonce);

        await transportC.StartAsync(default);
        await peerExplorerA.PingAsync(peerExplorerB.Peer, default);
        await peerExplorerB.PingAsync(peerExplorerA.Peer, default);
        await peerExplorerB.PingAsync(peerExplorerC.Peer, default);

        TestUtils.InvokeDelay(() => peerExplorerA.BroadcastTransaction([tx3, tx4]), 100);
        await syncServiceB.Staged.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await syncServiceC.Staged.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        // SwarmB receives tx3 and is staged, but policy filters it.
        Assert.Contains(tx3.Id, blockchainB.StagedTransactions.Keys);
        Assert.Contains(tx4.Id, blockchainB.StagedTransactions.Keys);

        // SwarmC can not receive tx3 because SwarmB does not rebroadcast it.
        Assert.DoesNotContain(tx3.Id, blockchainC.StagedTransactions.Keys);
        Assert.Contains(tx4.Id, blockchainC.StagedTransactions.Keys);
    }

    [Fact(Timeout = Timeout)]
    public async Task CanBroadcastBlock()
    {
        var keyA = PrivateKey.Parse("8568eb6f287afedece2c7b918471183db0451e1a61535bb0381cfdf95b85df20");
        var keyB = PrivateKey.Parse("c34f7498befcc39a14f03b37833f6c7bb78310f1243616524eda70e078b8313c");
        var keyC = PrivateKey.Parse("941bc2edfab840d79914d80fe3b30840628ac37a5d812d7f922b5d2405a223d3");
        using var fx = new MemoryRepositoryFixture();

        var transportA = TestUtils.CreateTransport(keyA);
        var transportB = TestUtils.CreateTransport(keyB);
        var transportC = TestUtils.CreateTransport(keyC);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var peerExplorerC = new PeerExplorer(transportC, peersC);
        var blockchainA = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainB = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainC = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var syncResponderServiceA = new BlockSynchronizationResponderService(blockchainA, transportA);
        var syncResponderServiceB = new BlockSynchronizationResponderService(blockchainB, transportB);
        var syncServiceA = new BlockSynchronizationService(blockchainA, transportA);
        var syncServiceB = new BlockSynchronizationService(blockchainB, transportB);
        var syncServiceC = new BlockSynchronizationService(blockchainC, transportC);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
        };
        await using var services = new ServiceCollection
        {
            syncResponderServiceA,
            syncResponderServiceB,
            syncServiceA,
            syncServiceB,
            syncServiceC,
        };

        blockchainA.ProposeAndAppendMany(keyA, 10);
        blockchainA.AppendTo(blockchainB, 1..5);

        await transports.StartAsync(default);
        await services.StartAsync(default);

        await Task.WhenAll(
            peerExplorerB.ExploreAsync([peerExplorerA.Peer], 3, default),
            peerExplorerC.ExploreAsync([peerExplorerA.Peer], 3, default));

        TestUtils.InvokeDelay(() => peerExplorerB.BroadcastBlock(blockchainB), 100);
        await Task.WhenAll(
            syncServiceA.BlockDemands.Added.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5)),
            syncServiceC.Appended.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.NotEqual(blockchainB.Tip, blockchainA.Tip);

        TestUtils.InvokeDelay(() => peerExplorerA.BroadcastBlock(blockchainA), 100);
        await Task.WhenAll(
            syncServiceB.Appended.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5)),
            syncServiceC.Appended.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(blockchainA.Blocks.Keys, blockchainB.Blocks.Keys);
        Assert.Equal(blockchainA.Blocks.Keys, blockchainC.Blocks.Keys);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastBlockWithSkip()
    {
        var blockchainOptions = new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
        };
        using var fx = new MemoryRepositoryFixture();
        var privateKey = new PrivateKey();
        var transportA = TestUtils.CreateTransport(privateKey);
        var transportB = TestUtils.CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var blockchainA = MakeBlockchain(blockchainOptions);
        var blockchainB = MakeBlockchain(blockchainOptions);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var syncResponderServiceA = new BlockSynchronizationResponderService(blockchainA, transportA);
        var syncServiceB = new BlockSynchronizationService(blockchainB, transportB);
        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };
        await using var services = new ServiceCollection
        {
            syncResponderServiceA,
            syncServiceB,
        };

        var renderCount = 0;
        using var _ = blockchainB.ActionExecuted.Subscribe(e =>
        {
            if (e.Action is DumbAction)
            {
                renderCount++;
            }
        });

        blockchainA.StagedTransactions.Add(privateKey, new TransactionSubmission
        {
            Actions =
            [
                DumbAction.Create((fx.Address2, "foo")),
                DumbAction.Create((fx.Address2, "bar"))
            ],
        });
        blockchainA.ProposeAndAppend(GenesisProposer);
        blockchainA.StagedTransactions.Add(privateKey, new TransactionSubmission
        {
            Actions =
            [
                DumbAction.Create((fx.Address2, "baz")),
                DumbAction.Create((fx.Address2, "qux"))
            ],
        });
        blockchainA.ProposeAndAppend(GenesisProposer);

        await transports.StartAsync(default);
        await services.StartAsync(default);

        await peerExplorerB.PingAsync(transportA.Peer, default);

        TestUtils.InvokeDelay(() => peerExplorerA.BroadcastBlock(blockchainA), 100);
        await syncServiceB.Appended.WaitAsync().WaitAsync(TimeSpan.FromSeconds(50));

        Assert.Equal(3, blockchainB.Blocks.Count);
        Assert.Equal(4, renderCount);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastBlockWithoutGenesis()
    {
        var keyA = new PrivateKey();
        var keyB = new PrivateKey();

        var transportA = TestUtils.CreateTransport(keyA);
        var transportB = TestUtils.CreateTransport(keyB);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var blockchainA = MakeBlockchain();
        var blockchainB = MakeBlockchain();
        var syncResponderServiceA = new BlockSynchronizationResponderService(blockchainA, transportA);
        var syncServiceB = new BlockSynchronizationService(blockchainB, transportB);
        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };
        await using var services = new ServiceCollection
        {
            syncResponderServiceA,
            syncServiceB,
        };

        await transports.StartAsync(default);
        await services.StartAsync(default);

        await peerExplorerB.PingAsync(transportA.Peer, default);

        blockchainA.ProposeAndAppend(keyA);
        TestUtils.InvokeDelay(() => peerExplorerA.BroadcastBlock(blockchainA), 100);
        await syncServiceB.Appended.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(blockchainB.Blocks.Keys, blockchainA.Blocks.Keys);

        blockchainA.ProposeAndAppend(keyB);
        TestUtils.InvokeDelay(() => peerExplorerA.BroadcastBlock(blockchainA), 100);
        await syncServiceB.Appended.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(blockchainB.Blocks.Keys, blockchainA.Blocks.Keys);
    }

    [Fact(Timeout = Timeout)]
    public async Task IgnoreExistingBlocks()
    {
        var keyA = new PrivateKey();
        var keyB = new PrivateKey();
        var transportA = TestUtils.CreateTransport(keyA);
        var transportB = TestUtils.CreateTransport(keyB);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        var blockchainA = MakeBlockchain();
        var blockchainB = MakeBlockchain();
        var syncResponderServiceA = new BlockSynchronizationResponderService(blockchainA, transportA);
        var syncServiceB = new BlockSynchronizationService(blockchainB, transportB);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };
        await using var services = new ServiceCollection
        {
            syncResponderServiceA,
            syncServiceB,
        };

        blockchainA.ProposeAndAppend(keyA);
        blockchainA.AppendTo(blockchainB, 1..);

        blockchainA.ProposeAndAppendMany(keyA, 3);

        await transports.StartAsync(default);
        await services.StartAsync(default);

        await peerExplorerB.PingAsync(transportA.Peer, default);

        TestUtils.InvokeDelay(() => peerExplorerA.BroadcastBlock(blockchainA), 100);
        await syncServiceB.Appended.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(blockchainA.Blocks.Keys, blockchainB.Blocks.Keys);

        TestUtils.InvokeDelay(() => peerExplorerA.BroadcastBlock(blockchainA), 100);
        await Assert.ThrowsAsync<TimeoutException>(
            async () => await syncServiceB.Appended.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact(Timeout = Timeout)]
    public async Task PullBlocks()
    {
        var keyA = new PrivateKey();
        var keyB = new PrivateKey();
        var keyC = new PrivateKey();
        using var fx = new MemoryRepositoryFixture();

        var transportA = TestUtils.CreateTransport(keyA);
        var transportB = TestUtils.CreateTransport(keyB);
        var transportC = TestUtils.CreateTransport(keyC);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);

        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        using var peerExplorerC = new PeerExplorer(transportC, peersC);

        var blockchainA = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainB = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainC = MakeBlockchain(genesisBlock: fx.GenesisBlock);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
        };

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

        // var blockDemandCollector = new BlockDemandCollector(blockchainC, transportC);
        var blockBranches = new BlockBranchCollection();
        using var blockFetcher = new BlockFetcher(blockchainC, transportC);
        using var blockBranchResolver = new BlockBranchResolver(blockchainC, blockFetcher);
        using var _1 = blockBranchResolver.BlockBranchCreated
            .Subscribe(e => blockBranches.Add(e.BlockBranch));
        var blockBranchAppender = new BlockBranchAppender(blockchainC);

        // await blockDemandCollector.ExecuteAsync([.. peerExplorerC.Peers], default);
        // await blockBranchResolver.ResolveAsync(blockDemandCollector.BlockDemands, blockchainC.Tip, default);
        await blockBranchAppender.AppendAsync(blockBranches, default);

        Assert.Equal(blockchainC.Tip, tipA);
    }

    [Fact(Timeout = Timeout)]
    public async Task CanFillWithInvalidTransaction()
    {
        var privateKey = new PrivateKey();
        var address = privateKey.Address;

        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var blockchainA = MakeBlockchain();
        var blockchainB = MakeBlockchain();
        var transactionResponderServiceB = new TransactionSynchronizationResponderService(blockchainB, transportB);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };
        await using var services = new ServiceCollection
        {
            transactionResponderServiceB,
        };

        var tx1 = blockchainB.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = [DumbAction.Create((address, "foo"))],
        });

        var tx2 = blockchainB.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = [DumbAction.Create((address, "bar"))],
        });

        var tx3 = blockchainB.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = [DumbAction.Create((address, "quz"))],
        });

        var tx4 = new TransactionBuilder
        {
            Nonce = 4,
            GenesisHash = blockchainA.Genesis.BlockHash,
            Actions = [DumbAction.Create((address, "qux"))],
        }.Create(privateKey);

        await transports.StartAsync(default);
        await services.StartAsync(default);

        await peerExplorerA.PingAsync(peerExplorerB.Peer, default);

        var request = new TransactionRequestMessage { TxIds = [tx1.Id, tx2.Id, tx3.Id, tx4.Id] };
        var response = await transportA.SendAsync<TransactionResponseMessage>(transportB.Peer, request, default);

        Assert.Equal(
            new[] { tx1, tx2, tx3 }.ToHashSet(),
            [.. response.Transactions]);
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotSpawnMultipleTaskForSinglePeer()
    {
        var key = new PrivateKey();

        var transportB = TestUtils.CreateTransport();
        var transportA = TestUtils.CreateTransport();

        var blockchainB = MakeBlockchain();
        var syncServiceB = new BlockSynchronizationService(blockchainB, transportB);
        var requestCount = 0;

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };
        await using var services = new ServiceCollection
        {
            syncServiceB,
        };

        using var _1 = transportA.MessageRouter.Register(new PingMessageHandler(transportA));
        using var _2 = transportA.MessageRouter.Register<IMessage>(e =>
        {
            if (e is BlockHashRequestMessage)
            {
                requestCount++;
            }
        });
        using var messageWaiterB = new MessageWaiter();
        using var _3 = transportB.MessageRouter.Register(messageWaiterB);

        var block1 = ProposeNextBlock(blockchainB.Genesis, key);
        var block2 = ProposeNextBlock(block1, key);

        await transports.StartAsync(default);
        await services.StartAsync(default);

        var message1 = new BlockSummaryMessage
        {
            GenesisHash = blockchainB.Genesis.BlockHash,
            BlockSummary = block1
        };

        InvokeAfter(() => transportA.Post(transportB.Peer, message1), TimeSpan.FromMilliseconds(100));

        await messageWaiterB.Received.WaitAsync(m => m is BlockHashRequestMessage).WaitAsync(TimeSpan.FromSeconds(5));

        InvokeAfter(() => transportA.Post(transportB.Peer, message1), TimeSpan.FromMilliseconds(100));

        await messageWaiterB.Received.WaitAsync(m => m is BlockHashRequestMessage).WaitAsync(TimeSpan.FromSeconds(5));

        var message2 = new BlockSummaryMessage
        {
            GenesisHash = blockchainB.Genesis.BlockHash,
            BlockSummary = block2
        };

        InvokeAfter(() => transportA.Post(transportB.Peer, message2), TimeSpan.FromMilliseconds(100));
        await messageWaiterB.Received.WaitAsync(m => m is BlockHashRequestMessage).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, requestCount);

        void InvokeAfter(Action action, TimeSpan delay)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(delay);
                action();
            });
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastEvidence()
    {
        var minerA = new PrivateKey();
        var validatorAddress = new PrivateKey().Address;
        var transportA = TestUtils.CreateTransport(minerA);
        var transportB = TestUtils.CreateTransport();
        var transportC = TestUtils.CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var peerExplorerC = new PeerExplorer(transportC, peersC);
        var blockchainA = MakeBlockchain();
        var blockchainB = MakeBlockchain();
        var blockchainC = MakeBlockchain();

        var syncResponderServiceA = new EvidenceSynchronizationResponderService(blockchainA, transportA);
        var syncServiceB = new EvidenceSynchronizationService(blockchainB, transportB);
        var syncServiceC = new EvidenceSynchronizationService(blockchainC, transportC);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
        };
        await using var services = new ServiceCollection
        {
            syncResponderServiceA,
            syncServiceB,
            syncServiceC,
        };

        var evidence = TestEvidence.Create(0, validatorAddress, DateTimeOffset.UtcNow);
        blockchainA.PendingEvidence.Add(evidence);

        await transports.StartAsync(default);
        await services.StartAsync(default);

        await peerExplorerA.PingAsync(peerExplorerB.Peer, default);
        await peerExplorerB.PingAsync(peerExplorerC.Peer, default);
        await peerExplorerC.PingAsync(peerExplorerA.Peer, default);

        TestUtils.InvokeDelay(() => peerExplorerA.BroadcastEvidence([evidence]), 100);
        await Task.WhenAll(
            syncServiceB.EvidenceAdded.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5)),
            syncServiceC.EvidenceAdded.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(evidence, blockchainB.PendingEvidence[evidence.Id]);
        Assert.Equal(evidence, blockchainC.PendingEvidence[evidence.Id]);
    }
}
