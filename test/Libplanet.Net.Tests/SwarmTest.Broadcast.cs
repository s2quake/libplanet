using System.Reactive.Linq;
using Libplanet.Extensions;
using Libplanet.Net.Components;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Messages;
using Libplanet.Net.Services;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Tests;
using Libplanet.TestUtilities;
using Libplanet.Types;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests;

public partial class SwarmTest
{
    [Fact(Timeout = Timeout)]
    public async Task BroadcastBlock()
    {
        const int blockCount = 5;
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var blockchainA = new Blockchain(genesisBlock: genesisBlock);
        var blockchainB = new Blockchain(genesisBlock: genesisBlock);
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
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var miner = RandomUtility.Signer(random);
        var signer = RandomUtility.Signer(random);
        var blockchainOptions = new BlockchainOptions();
        var blockchain = new Blockchain(genesisBlock, blockchainOptions);
        var seedBlockchain = new Blockchain(genesisBlock, blockchainOptions);
        var blockchainA = new Blockchain(genesisBlock, blockchainOptions);
        var blockchainB = new Blockchain(genesisBlock, blockchainOptions);

        var transport = CreateTransport();
        var transportA = CreateTransport(signer);
        var transportB = CreateTransport(signer);
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

        blockchain.ProposeAndAppendMany(miner, 10);
        blockchain.AppendTo(seedBlockchain, 1..5);

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        await peerExplorerA.PingAsync(transport.Peer, cancellationToken);
        await transportA.StopAsync(cancellationToken);
        await peerExplorer.RefreshAsync(TimeSpan.Zero, cancellationToken);

        Assert.DoesNotContain(transportA.Peer, peerExplorer.Peers);

        blockchain.AppendTo(seedBlockchain, 5..);

        await peerExplorerB.PingAsync(transport.Peer, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var signerA = RandomUtility.Signer(random);

        var transportA = CreateTransport(signerA);
        var transportB = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);

        var blockchainA = new Blockchain(new GenesisBlockBuilder { }.Create(proposer));
        var blockchainB = new Blockchain(new GenesisBlockBuilder { }.Create(proposer));
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

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        await peerExplorerB.PingAsync(transportA.Peer, cancellationToken);

        blockchainA.ProposeAndAppend(signerA);
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
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var minerA = RandomUtility.Signer(random);
        var minerB = RandomUtility.Signer(random);
        var transportA = CreateTransport(minerA);
        var transportB = CreateTransport(minerB);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        var blockchainA = new Blockchain(genesisBlock);
        var blockchainB = new Blockchain(genesisBlock);
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

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        await peerExplorerA.PingAsync(transportB.Peer, cancellationToken);

        await Task.Run(async () =>
        {
            while (blockchainA.Tip.Height < 10)
            {
                var interval = RandomUtility.TimeSpan(100, 1000);
                await Task.Delay(interval);
                blockchainA.ProposeAndAppend(minerA);
            }
        }, cancellationToken);

        using var cancellationSource = new CancellationTokenSource(2000);
        await syncServiceB.Appended.WaitAsync(e => e.Blocks[^1].Height == 10, cancellationSource.Token);

        Assert.Equal(blockchainA.Blocks.Keys, blockchainB.Blocks.Keys);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastTx()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var signerA = RandomUtility.Signer(random);
        var transportA = CreateTransport(signerA);
        var transportB = CreateTransport();
        var transportC = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var peerExplorerC = new PeerExplorer(transportC, peersC);

        var blockchainA = new Blockchain(genesisBlock: genesisBlock);
        var blockchainB = new Blockchain(genesisBlock: genesisBlock);
        var blockchainC = new Blockchain(genesisBlock: genesisBlock);

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

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        await peerExplorerA.PingAsync(transportB.Peer, cancellationToken);
        await peerExplorerB.PingAsync(transportC.Peer, cancellationToken);
        await peerExplorerC.PingAsync(transportA.Peer, cancellationToken);

        var txSigner = new PrivateKey().AsSigner();
        var tx = blockchainA.CreateTransaction(txSigner);

        blockchainA.StagedTransactions.Add(tx);
        blockchainA.ProposeAndAppend(signerA);

        var waitTaskB = serviceB.Staged.WaitAsync();
        var waitTaskC = serviceC.Staged.WaitAsync();
        peerExplorerA.BroadcastTransaction([tx.Id]);

        await waitTaskB.WaitAsync(WaitTimeout3, cancellationToken);
        await waitTaskC.WaitAsync(WaitTimeout3, cancellationToken);

        Assert.Equal(tx, blockchainB.StagedTransactions[tx.Id]);
        Assert.Equal(tx, blockchainC.StagedTransactions[tx.Id]);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastTxWhileMining()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var signerC = RandomUtility.Signer(random);
        var transportA = CreateTransport();
        var transportC = CreateTransport(signerC);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerC = new PeerExplorer(transportC, peersC);
        var blockchainA = new Blockchain(genesisBlock: genesisBlock);
        var blockchainC = new Blockchain(genesisBlock: genesisBlock);
        var serviceA = new TransactionSynchronizationResponderService(blockchainA, transportA);
        var serviceC = new TransactionSynchronizationService(blockchainC, transportC);
        await using var services = new ServiceCollection
        {
            transportA,
            transportC,
            serviceA,
            serviceC
        };

        var signer = RandomUtility.Signer();
        var address = signer.Address;
        var txCount = 10;
        var txs = Enumerable.Range(0, txCount).Select(_ =>
                blockchainA.StagedTransactions.Add(RandomUtility.Signer(), new TransactionParams
                {
                    Actions = [DumbAction.Create((address, "foo"))],
                }))
            .ToImmutableArray();

        await services.StartAsync(cancellationToken);

        await peerExplorerC.PingAsync(transportA.Peer, cancellationToken);
        Assert.Contains(transportC.Peer, peerExplorerA.Peers);
        Assert.Contains(transportA.Peer, peerExplorerC.Peers);

        var miningTask = Task.Run(async () =>
        {
            for (var i = 0; i < 10; i++)
            {
                blockchainC.ProposeAndAppend(signerC);
                await Task.Delay(100);
            }
        }, cancellationToken);

        for (var i = 0; i < 100; i++)
        {
            peerExplorerA.BroadcastTransaction(txs);
        }

        var waitTaskC = serviceC.Staged.WaitAsync(
            _ => blockchainC.StagedTransactions.Count == txCount);
        await miningTask;
        await waitTaskC.WaitAsync(WaitTimeout5, cancellationToken);

        Assert.All(txs, tx => Assert.Contains(tx.Id, blockchainC.Transactions.Keys));
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastTxAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var transportC = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var peerExplorerC = new PeerExplorer(transportC, peersC);
        var blockchainA = new Blockchain(genesisBlock: genesisBlock);
        var blockchainB = new Blockchain(genesisBlock: genesisBlock);
        var blockchainC = new Blockchain(genesisBlock: genesisBlock);

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

        var txSigner = new PrivateKey().AsSigner();
        var tx = blockchainA.CreateTransaction(txSigner);

        blockchainA.StagedTransactions.Add(tx);

        await services.StartAsync(cancellationToken);

        var waitTaskB = syncServiceB.Staged.WaitAsync();
        await peerExplorerA.PingAsync(peerExplorerB.Peer, cancellationToken);

        await waitTaskB.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(tx, blockchainB.StagedTransactions[tx.Id]);

        await transportA.DisposeAsync();

        var waitTaskC = syncServiceC.Staged.WaitAsync();
        await peerExplorerB.PingAsync(peerExplorerC.Peer, cancellationToken);

        await waitTaskC.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(tx, blockchainC.StagedTransactions[tx.Id]);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastTxAsyncMany()
    {
        const int size = 5;

        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
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
            transports[i] = CreateTransport();
            peerses[i] = new PeerCollection(transports[i].Peer.Address);
            peerExplorers[i] = new PeerExplorer(transports[i], peerses[i]);
            blockchains[i] = new Blockchain(genesisBlock: genesisBlock);
            broadcastServices[i] = new TransactionBroadcastService(blockchains[i], peerExplorers[i]);
            syncResponseServices[i] = new TransactionSynchronizationResponderService(blockchains[i], transports[i]);
            syncServices[i] = new TransactionSynchronizationService(blockchains[i], transports[i]);

            services.Add(transports[i]);
            services.Add(broadcastServices[i]);
            services.Add(syncResponseServices[i]);
            services.Add(syncServices[i]);
        }

        var txSigner = new PrivateKey().AsSigner();
        var tx = blockchains[size - 1].CreateTransaction(txSigner);
        blockchains[size - 1].StagedTransactions.Add(tx);

        await services.StartAsync(cancellationToken);

        var waitTaskList = new List<Task>();
        for (var i = 0; i < size - 1; i++)
        {
            waitTaskList.Add(syncServices[i].Staged.WaitAsync());
        }

        for (var i = 1; i < size; i++)
        {
            await peerExplorers[i].PingAsync(peerExplorers[0].Peer, cancellationToken);
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
        var cancellationToken = TestContext.Current.CancellationToken;
        var signerA = PrivateKey.Parse("8568eb6f287afedece2c7b918471183db0451e1a61535bb0381cfdf95b85df20").AsSigner();
        var signerB = PrivateKey.Parse("c34f7498befcc39a14f03b37833f6c7bb78310f1243616524eda70e078b8313c").AsSigner();
        var signerC = PrivateKey.Parse("941bc2edfab840d79914d80fe3b30840628ac37a5d812d7f922b5d2405a223d3").AsSigner();
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        await using var transportA = CreateTransport(signerA);
        await using var transportB = CreateTransport(signerB);
        await using var transportC = CreateTransport(signerC);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var peerExplorerC = new PeerExplorer(transportC, peersC);
        var blockchainA = new Blockchain(genesisBlock: genesisBlock);
        var blockchainB = new Blockchain(genesisBlock: genesisBlock);
        var blockchainC = new Blockchain(genesisBlock: genesisBlock);
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

        var signer = RandomUtility.Signer();
        var tx1 = blockchainA.StagedTransactions.Add(signer);
        var tx2 = blockchainA.StagedTransactions.Add(signer);
        Assert.Equal(0, tx1.Nonce);
        Assert.Equal(1, tx2.Nonce);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);
        await peerExplorerA.PingAsync(peerExplorerB.Peer, cancellationToken);
        InvokeDelay(() => peerExplorerA.BroadcastTransaction([tx1, tx2]), 100);
        await syncServiceB.Staged.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(
            new HashSet<TxId> { tx1.Id, tx2.Id },
            [.. blockchainB.StagedTransactions.Keys]);
        peerExplorerA.Peers.Remove(peerExplorerB.Peer);
        peerExplorerB.Peers.Remove(peerExplorerA.Peer);

        Assert.Equal(2, blockchainA.GetNextTxNonce(signer.Address));
        blockchainA.StagedTransactions.Remove(tx2.Id);
        Assert.Equal(1, blockchainA.GetNextTxNonce(signer.Address));

        await transportA.StopAsync(cancellationToken);
        await transportB.StopAsync(cancellationToken);

        peerExplorerA.Peers.Remove(peerExplorerB.Peer);
        peerExplorerB.Peers.Remove(peerExplorerA.Peer);
        Assert.Empty(peerExplorerA.Peers);
        Assert.Empty(peerExplorerB.Peers);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);

        blockchainB.ProposeAndAppend(signerB);

        var tx3 = blockchainA.StagedTransactions.Add(signer);
        var tx4 = blockchainA.StagedTransactions.Add(signer);
        Assert.Equal(1, tx3.Nonce);
        Assert.Equal(2, tx4.Nonce);

        await transportC.StartAsync(cancellationToken);
        await peerExplorerA.PingAsync(peerExplorerB.Peer, cancellationToken);
        await peerExplorerB.PingAsync(peerExplorerA.Peer, cancellationToken);
        await peerExplorerB.PingAsync(peerExplorerC.Peer, cancellationToken);

        InvokeDelay(() => peerExplorerA.BroadcastTransaction([tx3, tx4]), 100);
        await syncServiceB.Staged.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken);
        await syncServiceC.Staged.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var signerA = PrivateKey.Parse("8568eb6f287afedece2c7b918471183db0451e1a61535bb0381cfdf95b85df20").AsSigner();
        var signerB = PrivateKey.Parse("c34f7498befcc39a14f03b37833f6c7bb78310f1243616524eda70e078b8313c").AsSigner();
        var signerC = PrivateKey.Parse("941bc2edfab840d79914d80fe3b30840628ac37a5d812d7f922b5d2405a223d3").AsSigner();

        var transportA = CreateTransport(signerA);
        var transportB = CreateTransport(signerB);
        var transportC = CreateTransport(signerC);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var peerExplorerC = new PeerExplorer(transportC, peersC);
        var blockchainA = new Blockchain(genesisBlock);
        var blockchainB = new Blockchain(genesisBlock);
        var blockchainC = new Blockchain(genesisBlock);
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

        blockchainA.ProposeAndAppendMany(signerA, 10);
        blockchainA.AppendTo(blockchainB, 1..5);

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        await Task.WhenAll(
            peerExplorerB.ExploreAsync([peerExplorerA.Peer], 3, cancellationToken),
            peerExplorerC.ExploreAsync([peerExplorerA.Peer], 3, cancellationToken));

        InvokeDelay(() => peerExplorerB.BroadcastBlock(blockchainB), 100);
        await Task.WhenAll(
            syncServiceA.BlockDemands.Added.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken),
            syncServiceC.Appended.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken));

        Assert.NotEqual(blockchainB.Tip, blockchainA.Tip);

        InvokeDelay(() => peerExplorerA.BroadcastBlock(blockchainA), 100);
        await Task.WhenAll(
            syncServiceB.Appended.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken),
            syncServiceC.Appended.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken));

        Assert.Equal(blockchainA.Blocks.Keys, blockchainB.Blocks.Keys);
        Assert.Equal(blockchainA.Blocks.Keys, blockchainC.Blocks.Keys);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastBlockWithSkip()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var blockchainOptions = new BlockchainOptions
        {
            SystemAction = new SystemAction
            {
                LeaveBlockActions = [new MinerReward(1)],
            },
        };
        var signer = RandomUtility.Signer(random);
        var proposer = RandomUtility.Signer(random);
        var address = RandomUtility.Address(random);
        var transportA = CreateTransport(signer);
        var transportB = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchainA = new Blockchain(genesisBlock, blockchainOptions);
        var blockchainB = new Blockchain(genesisBlock, blockchainOptions);
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

        blockchainA.StagedTransactions.Add(signer, new TransactionParams
        {
            Actions =
            [
                DumbAction.Create((address, "foo")),
                DumbAction.Create((address, "bar"))
            ],
        });
        blockchainA.ProposeAndAppend(proposer);
        blockchainA.StagedTransactions.Add(signer, new TransactionParams
        {
            Actions =
            [
                DumbAction.Create((address, "baz")),
                DumbAction.Create((address, "qux"))
            ],
        });
        blockchainA.ProposeAndAppend(proposer);

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        await peerExplorerB.PingAsync(transportA.Peer, cancellationToken);

        InvokeDelay(() => peerExplorerA.BroadcastBlock(blockchainA), 100);
        await syncServiceB.Appended.WaitAsync().WaitAsync(TimeSpan.FromSeconds(50), cancellationToken);

        Assert.Equal(3, blockchainB.Blocks.Count);
        Assert.Equal(4, renderCount);
    }

    [Fact(Timeout = Timeout)]
    public async Task BroadcastBlockWithoutGenesis()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var signerA = RandomUtility.Signer(random);
        var signerB = RandomUtility.Signer(random);

        var transportA = CreateTransport(signerA);
        var transportB = CreateTransport(signerB);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchainA = new Blockchain(genesisBlock);
        var blockchainB = new Blockchain(genesisBlock);
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

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        await peerExplorerB.PingAsync(transportA.Peer, cancellationToken);

        blockchainA.ProposeAndAppend(signerA);
        InvokeDelay(() => peerExplorerA.BroadcastBlock(blockchainA), 100);
        await syncServiceB.Appended.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken);

        Assert.Equal(blockchainB.Blocks.Keys, blockchainA.Blocks.Keys);

        blockchainA.ProposeAndAppend(signerB);
        InvokeDelay(() => peerExplorerA.BroadcastBlock(blockchainA), 100);
        await syncServiceB.Appended.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken);

        Assert.Equal(blockchainB.Blocks.Keys, blockchainA.Blocks.Keys);
    }

    [Fact(Timeout = Timeout)]
    public async Task IgnoreExistingBlocks()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var signerA = RandomUtility.Signer(random);
        var signerB = RandomUtility.Signer(random);
        var transportA = CreateTransport(signerA);
        var transportB = CreateTransport(signerB);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchainA = new Blockchain(genesisBlock);
        var blockchainB = new Blockchain(genesisBlock);
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

        blockchainA.ProposeAndAppend(signerA);
        blockchainA.AppendTo(blockchainB, 1..);

        blockchainA.ProposeAndAppendMany(signerA, 3);

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        await peerExplorerB.PingAsync(transportA.Peer, cancellationToken);

        InvokeDelay(() => peerExplorerA.BroadcastBlock(blockchainA), 100);
        await syncServiceB.Appended.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken);

        Assert.Equal(blockchainA.Blocks.Keys, blockchainB.Blocks.Keys);

        InvokeDelay(() => peerExplorerA.BroadcastBlock(blockchainA), 100);
        await Assert.ThrowsAsync<TimeoutException>(
            () => syncServiceB.Appended.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken));
    }

    [Fact(Timeout = Timeout)]
    public async Task PullBlocks()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var signerA = RandomUtility.Signer(random);
        var signerB = RandomUtility.Signer(random);
        var signerC = RandomUtility.Signer(random);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);

        var transportA = CreateTransport(signerA);
        var transportB = CreateTransport(signerB);
        var transportC = CreateTransport(signerC);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);

        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        using var peerExplorerC = new PeerExplorer(transportC, peersC);

        var blockchainA = new Blockchain(genesisBlock);
        var blockchainB = new Blockchain(genesisBlock);
        var blockchainC = new Blockchain(genesisBlock);

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

        blockchainA.ProposeAndAppendMany(signerA, 5);
        blockchainA.AppendTo(blockchainB, new Range(1, 3));

        var tipA = blockchainA.Tip;

        for (var i = 0; i < 10; i++)
        {
            var block = blockchainB.Propose(signerB);
            blockchainB.Append(block, CreateBlockCommit(block));
        }

        await transports.StartAsync(cancellationToken);

        await peerExplorerB.PingAsync(transportA.Peer, cancellationToken);
        await peerExplorerC.PingAsync(transportA.Peer, cancellationToken);
        await peerExplorerB.ExploreAsync([transportA.Peer], 3, cancellationToken);
        await peerExplorerC.ExploreAsync([transportA.Peer], 3, cancellationToken);

        var blockBranches = new BlockBranchCollection();
        using var blockFetcher = new BlockFetcher(blockchainC, transportC);
        using var blockBranchResolver = new BlockBranchResolver(blockchainC, blockFetcher);
        using var _1 = blockBranchResolver.BlockBranchCreated
            .Subscribe(e => blockBranches.Add(e.BlockBranch));
        var blockBranchAppender = new BlockBranchAppender(blockchainC);

        await blockBranchAppender.AppendAsync(blockBranches, cancellationToken);

        Assert.Equal(blockchainC.Tip, tipA);
    }

    [Fact(Timeout = Timeout)]
    public async Task CanFillWithInvalidTransaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var signer = RandomUtility.Signer(random);
        var address = signer.Address;

        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var blockchainA = new Blockchain(genesisBlock);
        var blockchainB = new Blockchain(genesisBlock);
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

        var tx1 = blockchainB.StagedTransactions.Add(signer, @params: new()
        {
            Actions = [DumbAction.Create((address, "foo"))],
        });

        var tx2 = blockchainB.StagedTransactions.Add(signer, @params: new()
        {
            Actions = [DumbAction.Create((address, "bar"))],
        });

        var tx3 = blockchainB.StagedTransactions.Add(signer, @params: new()
        {
            Actions = [DumbAction.Create((address, "quz"))],
        });

        var tx4 = new TransactionBuilder
        {
            Nonce = 4,
            GenesisBlockHash = blockchainA.Genesis.BlockHash,
            Actions = [DumbAction.Create((address, "qux"))],
        }.Create(signer);

        Assert.Equal(4, tx4.Nonce);

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        await peerExplorerA.PingAsync(peerExplorerB.Peer, cancellationToken);

        var request = new TransactionRequestMessage { TxIds = [tx1.Id, tx2.Id, tx3.Id, tx4.Id] };
        var response = await transportA.SendAsync<TransactionResponseMessage>(
            transportB.Peer, request, cancellationToken);

        Assert.Equal(
            new[] { tx1, tx2, tx3 }.ToHashSet(),
            [.. response.Transactions]);
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotSpawnMultipleTaskForSinglePeer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var signer = RandomUtility.Signer(random);

        var transportB = CreateTransport();
        var transportA = CreateTransport();

        var blockchainB = new Blockchain(genesisBlock);
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

        var block1 = new BlockBuilder
        {
            PreviousBlockHash = blockchainB.Tip.BlockHash,
            PreviousStateRootHash = blockchainB.StateRootHash,
        }.Create(signer);
        var block2 = new BlockBuilder
        {
            PreviousBlockHash = block1.BlockHash,
            PreviousStateRootHash = blockchainB.StateRootHash,
        }.Create(signer);

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        var message1 = new BlockSummaryMessage
        {
            GenesisBlockHash = blockchainB.Genesis.BlockHash,
            BlockSummary = block1
        };

        InvokeAfter(() => transportA.Post(transportB.Peer, message1), TimeSpan.FromMilliseconds(100));

        await messageWaiterB.Received.WaitAsync(m => m is BlockHashRequestMessage)
            .WaitAsync(WaitTimeout5, cancellationToken);

        InvokeAfter(() => transportA.Post(transportB.Peer, message1), TimeSpan.FromMilliseconds(100));

        await messageWaiterB.Received.WaitAsync(m => m is BlockHashRequestMessage)
            .WaitAsync(WaitTimeout5, cancellationToken);

        var message2 = new BlockSummaryMessage
        {
            GenesisBlockHash = blockchainB.Genesis.BlockHash,
            BlockSummary = block2
        };

        InvokeAfter(() => transportA.Post(transportB.Peer, message2), TimeSpan.FromMilliseconds(100));
        await messageWaiterB.Received.WaitAsync(m => m is BlockHashRequestMessage)
            .WaitAsync(WaitTimeout5, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var minerA = RandomUtility.Signer(random);
        var validatorAddress = new PrivateKey().Address;
        var transportA = CreateTransport(minerA);
        var transportB = CreateTransport();
        var transportC = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var peerExplorerC = new PeerExplorer(transportC, peersC);
        var blockchainA = new Blockchain(genesisBlock);
        var blockchainB = new Blockchain(genesisBlock);
        var blockchainC = new Blockchain(genesisBlock);

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

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        await peerExplorerA.PingAsync(peerExplorerB.Peer, cancellationToken);
        await peerExplorerB.PingAsync(peerExplorerC.Peer, cancellationToken);
        await peerExplorerC.PingAsync(peerExplorerA.Peer, cancellationToken);

        InvokeDelay(() => peerExplorerA.BroadcastEvidence([evidence]), 100);
        await Task.WhenAll(
            syncServiceB.EvidenceAdded.WaitAsync(),
            syncServiceC.EvidenceAdded.WaitAsync())
            .WaitAsync(WaitTimeout5, cancellationToken);

        Assert.Equal(evidence, blockchainB.PendingEvidence[evidence.Id]);
        Assert.Equal(evidence, blockchainC.PendingEvidence[evidence.Id]);
    }
}
