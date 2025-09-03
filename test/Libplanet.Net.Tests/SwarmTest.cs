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
using Libplanet.Types.Threading;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests;

public partial class SwarmTest(ITestOutputHelper output)
{
    private const int Timeout = 60 * 1000;

    [Fact(Timeout = Timeout)]
    public async Task HandleReconnection()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var transport = CreateTransport();
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        var peers = new PeerCollection(transport.Peer.Address);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);

        var peerExplorer = new PeerExplorer(transport, peers);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);

        await transport.StartAsync(cancellationToken);
        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);
        await peerExplorerA.PingAsync(peerExplorer.Peer, cancellationToken);
        await transportA.StopAsync(cancellationToken);
        await peerExplorerB.PingAsync(peerExplorer.Peer, cancellationToken);

        Assert.Contains(transportB.Peer, peerExplorer.Peers);
        Assert.Contains(transport.Peer, peerExplorerB.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task AddPeersWithoutStart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var a = CreateTransport();
        await using var b = CreateTransport();
        var peersA = new PeerCollection(a.Peer.Address);
        var peersB = new PeerCollection(b.Peer.Address);
        var peerExplorerA = new PeerExplorer(a, peersA);
        var peerExplorerB = new PeerExplorer(b, peersB);

        await b.StartAsync(cancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => peerExplorerA.PingAsync(peerExplorerB.Peer, cancellationToken));
    }

    [Fact(Timeout = Timeout)]
    public async Task AddPeersAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var a = CreateTransport();
        await using var b = CreateTransport();
        var peersA = new PeerCollection(a.Peer.Address);
        var peersB = new PeerCollection(b.Peer.Address);
        var peerExplorerA = new PeerExplorer(a, peersA);
        var peerExplorerB = new PeerExplorer(b, peersB);

        await a.StartAsync(cancellationToken);
        await b.StartAsync(cancellationToken);

        await peerExplorerA.PingAsync(peerExplorerB.Peer, cancellationToken);

        Assert.Contains(a.Peer, peerExplorerB.Peers);
        Assert.Contains(b.Peer, peerExplorerA.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task BootstrapException()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        using var _ = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB)
        {
            SeedPeers = [transportA.Peer],
        };

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);
        await peerExplorerB.ExploreAsync(cancellationToken);
        Assert.Single(peerExplorerB.Peers, transportA.Peer);
    }

    [Fact(Timeout = Timeout)]
    public async Task BootstrapAsyncWithoutStart()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var transportA = CreateTransport();
        await using var transportB = CreateTransport();
        await using var transportC = CreateTransport();
        await using var transportD = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peersD = new PeerCollection(transportD.Peer.Address);
        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        using var peerExplorerC = new PeerExplorer(transportC, peersC);
        using var peerExplorerD = new PeerExplorer(transportD, peersD);

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);
        await transportC.StartAsync(cancellationToken);
        await transportD.StartAsync(cancellationToken);

        var bootstrappedAt = DateTimeOffset.UtcNow;
        peerExplorerC.Peers.AddOrUpdate(transportD.Peer);
        await peerExplorerB.ExploreAsync([transportA.Peer], 3, cancellationToken);
        await peerExplorerC.ExploreAsync([transportA.Peer], 3, cancellationToken);

        Assert.Contains(transportB.Peer, peerExplorerC.Peers);
        Assert.Contains(transportC.Peer, peerExplorerB.Peers);
        foreach (var peer in peerExplorerB.Peers)
        {
            var peerState = peerExplorerB.Peers.GetState(peer.Address);
            Assert.InRange(peerState.LastUpdated, bootstrappedAt, DateTimeOffset.UtcNow);
        }

        foreach (var peer in peerExplorerC.Peers)
        {
            var peerState = peerExplorerC.Peers.GetState(peer.Address);
            Assert.InRange(peerState.LastUpdated, bootstrappedAt, DateTimeOffset.UtcNow);
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task MaintainStaticPeers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var signerA = RandomUtility.Signer(random);

        await using var transportA = CreateTransport(signerA);
        await using var transportB = CreateTransport();

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);


        await using var transport = CreateTransport();

        var peers = new PeerCollection(transport.Peer.Address);
        var peerExplorer = new PeerExplorer(transport, peers)
        {
            SeedPeers = [transport.Peer],
        };
        var refreshService = new RefreshStaticPeersService(peerExplorer, [transportA.Peer, transportB.Peer]);

        await transport.StartAsync(cancellationToken);
        await refreshService.StartAsync(cancellationToken);

        await Task.WhenAll(
            refreshService.PeerAdded.WaitAsync(predicate: p => p == transportA.Peer),
            refreshService.PeerAdded.WaitAsync(predicate: p => p == transportB.Peer))
            .WaitAsync(WaitTimeout5, cancellationToken);

        await transportA.DisposeAsync();
        await Task.Delay(100, cancellationToken);
        await peerExplorer.RefreshAsync(TimeSpan.Zero, cancellationToken);
        // Invoke once more in case of swarmA and swarmB is in the same bucket,
        // and swarmA is last updated.
        await peerExplorer.RefreshAsync(TimeSpan.Zero, cancellationToken);
        Assert.DoesNotContain(transportA.Peer, peerExplorer.Peers);
        Assert.Contains(transportB.Peer, peerExplorer.Peers);

        var transportOptionsC = new TransportOptions
        {
            Port = transportA.Peer.EndPoint.Port,
        };
        await using var transportC = CreateTransport(signerA, options: transportOptionsC);
        await transportC.StartAsync(cancellationToken);

        await Task.WhenAll(
            refreshService.PeerAdded.WaitAsync(predicate: p => p == transportA.Peer),
            refreshService.PeerAdded.WaitAsync(predicate: p => p == transportB.Peer))
            .WaitAsync(WaitTimeout5, cancellationToken);
    }

    [Fact(Timeout = Timeout)]
    public async Task GetBlocks()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var signerA = RandomUtility.Signer(random);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchainA = new Blockchain(genesisBlock: genesisBlock);
        var blockchainB = new Blockchain(genesisBlock: genesisBlock);

        await using var transportA = CreateTransport(signerA);
        await using var transportB = CreateTransport();
        using var fetcherB = new BlockFetcher(blockchainB, transportB);

        var (block1, blockCommit1) = blockchainA.ProposeAndAppend(signerA);
        var (block2, blockCommit2) = blockchainA.ProposeAndAppend(signerA);

        transportA.MessageRouter.Register(new BlockHashRequestMessageHandler(blockchainA, transportA));
        transportA.MessageRouter.Register(new BlockRequestMessageHandler(blockchainA, transportA, 1));

        await transportA.StartAsync(cancellationToken);
        await transportB.StartAsync(cancellationToken);
        var blockHashes = ImmutableArray.CreateRange([block1.BlockHash, block2.BlockHash]);
        var blockPairs = await fetcherB.FetchAsync(transportA.Peer, blockHashes, cancellationToken);

        Assert.Equal([block1, block2], [.. blockPairs.Select(item => item.Item1)]);
        Assert.Equal([blockCommit1, blockCommit2], blockPairs.Select(item => item.Item2).ToArray());
    }

    [Fact(Timeout = Timeout)]
    public async Task GetMultipleBlocksAtOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var signerA = RandomUtility.Signer(random);
        var signerB = RandomUtility.Signer(random);

        var transportA = CreateTransport(signerA);
        var transportB = CreateTransport(signerB);
        _ = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var blockchainA = new Blockchain(genesisBlock);
        _ = new Blockchain(genesisBlock);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };

        blockchainA.ProposeAndAppend(signerA);
        blockchainA.ProposeAndAppend(signerA);

        transportA.MessageRouter.Register(new BlockHashRequestMessageHandler(blockchainA, transportA));
        transportA.MessageRouter.Register(new BlockRequestMessageHandler(blockchainA, transportA, 2));

        await transports.StartAsync(cancellationToken);

        peersB.AddOrUpdate(transportA.Peer);

        var hashes = await transportB.GetBlockHashesAsync(
            transportA.Peer,
            blockchainA.Genesis.BlockHash,
            cancellationToken);

        var request = new BlockRequestMessage { BlockHashes = [.. hashes], ChunkSize = 2 };
        var responses = await transportB.SendAsync<BlockResponseMessage>(
            transportA.Peer, request, response => response.IsLast, cancellationToken).ToArrayAsync(cancellationToken);

        var response0 = responses[0];
        Assert.Equal(2, responses.Length);
        Assert.Equal(2, response0.Blocks.Length);
        Assert.Equal(2, response0.BlockCommits.Length);

        var response1 = responses[1];
        Assert.Single(response1.Blocks);
        Assert.Single(response1.BlockCommits);
    }

    [Fact(Timeout = Timeout)]
    public async Task GetTx()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var signerB = RandomUtility.Signer(random);
        var transportA = CreateTransport();
        var transportB = CreateTransport(signerB);
        var blockchainA = new Blockchain(genesisBlock);
        var blockchainB = new Blockchain(genesisBlock);
        var fetcherA = new TransactionFetcher(blockchainA, transportA);
        var txSigner = RandomUtility.Signer(random);
        var tx = blockchainB.CreateTransaction(txSigner);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };

        blockchainB.StagedTransactions.Add(tx);
        blockchainB.ProposeAndAppend(signerB);

        transportB.MessageRouter.Register(new TransactionRequestMessageHandler(blockchainB, transportB, 1));

        await transports.StartAsync(cancellationToken);

        var txs = await fetcherA.FetchAsync(transportB.Peer, [tx.Id], cancellationToken);
        Assert.Equal(new[] { tx }, txs);
    }

    [Fact(Timeout = Timeout)]
    public async Task CannotBlockSyncWithForkedChain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var signerA = RandomUtility.Signer(random);
        var signerB = RandomUtility.Signer(random);
        var transportA = CreateTransport(signerA);
        var transportB = CreateTransport(signerB);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var blockchainA = new Blockchain(genesisBlock);
        var blockchainB = new Blockchain(genesisBlock);
        var syncServiceA = new BlockSynchronizationService(blockchainA, transportA);
        var syncResponderServiceB = new BlockSynchronizationResponderService(
            blockchainB, transportB);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };
        await using var services = new ServiceCollection
        {
            syncServiceA,
            syncResponderServiceB,
        };

        var signer = RandomUtility.Signer(random);
        var addr = transportA.Peer.Address;
        var item = "foo";

        blockchainA.StagedTransactions.Add(signer, @params: new()
        {
            Actions = [DumbAction.Create((addr, item))],
        });
        var (tipA, _) = blockchainA.ProposeAndAppend(signerA);

        blockchainB.StagedTransactions.Add(signer, @params: new()
        {
            Actions = [DumbAction.Create((addr, item))],
        });
        blockchainB.ProposeAndAppend(signerB);

        blockchainB.StagedTransactions.Add(signer, @params: new()
        {
            Actions = [DumbAction.Create((addr, item))],
        });
        var (tipB, _) = blockchainB.ProposeAndAppend(signerB);

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        peersB.Add(transportA.Peer);
        peerExplorerB.BroadcastBlock(blockchainB, tipB);

        await syncServiceA.FetchingFailed.WaitAsync().WaitAsync(WaitTimeout10, cancellationToken);
        Assert.Equal(tipA, blockchainA.Tip);
    }

    [Fact(Timeout = Timeout)]
    public async Task UnstageInvalidTransaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var validSigner = new PrivateKey().AsSigner();
        var blockchainOptions = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validators =
                [
                    new RelayObjectValidator<Transaction>(tx =>
                    {
                        var validAddress = validSigner.Address;
                        if (!tx.Signer.Equals(validAddress) && !tx.Signer.Equals(proposer.Address))
                        {
                            throw new InvalidOperationException("invalid signer");
                        }
                    }),
                ],
            },
        };
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);

        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var blockchainA = new Blockchain(genesisBlock, blockchainOptions);
        var blockchainB = new Blockchain(genesisBlock, blockchainOptions);

        var syncResponderServiceA = new TransactionSynchronizationResponderService(blockchainA, transportA);
        var syncServiceB = new TransactionSynchronizationService(blockchainB, transportB);

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

        var invalidSigner = RandomUtility.Signer(random);
        var validTx = blockchainA.StagedTransactions.Add(validSigner);
        var invalidTx = blockchainB.CreateTransaction(invalidSigner);
        Assert.Throws<InvalidOperationException>(() => blockchainA.StagedTransactions.Add(invalidSigner));

        await transports.StartAsync(cancellationToken);
        await services.StartAsync(cancellationToken);

        peersA.Add(transportB.Peer);

        peerExplorerA.BroadcastTransaction([validTx, invalidTx]);
        await syncServiceB.Staged.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken);

        Assert.Equal(blockchainB.StagedTransactions[validTx.Id], validTx);
        Assert.Throws<KeyNotFoundException>(
            () => blockchainB.Transactions[invalidTx.Id]);

        Assert.Contains(validTx.Id, blockchainB.StagedTransactions.Keys);
        Assert.DoesNotContain(invalidTx.Id, blockchainB.StagedTransactions.Keys);
    }

    [Fact(Timeout = Timeout)]
    public async Task IgnoreTransactionFromDifferentGenesis()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var validSigner = RandomUtility.Signer(random);
        var blockchainOptions = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validators =
                [
                    new RelayObjectValidator<Transaction>(tx =>
                    {
                        var validAddress = validSigner.Address;
                        if (!tx.Signer.Equals(validAddress) && !tx.Signer.Equals(proposer.Address))
                        {
                            throw new InvalidOperationException("invalid signer");
                        }
                    }),
                ],
            },
        };
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        using var peerExplorerA = new PeerExplorer(transportA, peersA);

        var genesisBlockA = new BlockBuilder { }.Create(validSigner);
        var genesisBlockB = new BlockBuilder { }.Create(validSigner);
        var blockchainA = new Blockchain(genesisBlockA, blockchainOptions);
        var blockchainB = new Blockchain(genesisBlockB, blockchainOptions);

        var syncResponderServiceA = new TransactionSynchronizationResponderService(blockchainA, transportA);
        var syncServiceB = new TransactionSynchronizationService(blockchainB, transportB);

        await using var services = new ServiceCollection
        {
            transportA,
            transportB,
            syncResponderServiceA,
            syncServiceB,
        };

        var tx = blockchainA.StagedTransactions.Add(validSigner);

        await services.StartAsync(cancellationToken);
        peersA.Add(transportB.Peer);

        peerExplorerA.BroadcastTransaction([tx]);
        await syncServiceB.StageFailed.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken);

        Assert.Throws<KeyNotFoundException>(() => blockchainB.Transactions[tx.Id]);
        Assert.DoesNotContain(tx.Id, blockchainB.StagedTransactions.Keys);
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotReceiveBlockFromNodeHavingDifferentGenesisBlock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var signerA = PrivateKey.Parse("8568eb6f287afedece2c7b918471183db0451e1a61535bb0381cfdf95b85df20").AsSigner();
        var signerB = PrivateKey.Parse("c34f7498befcc39a14f03b37833f6c7bb78310f1243616524eda70e078b8313c").AsSigner();
        var signerC = PrivateKey.Parse("941bc2edfab840d79914d80fe3b30840628ac37a5d812d7f922b5d2405a223d3").AsSigner();

        var signerAddress = RandomUtility.Address(random);

        var genesisBlockBuilderA = TestUtils.GenesisBlockBuilder with
        {
            Actions = [DumbAction.Create((signerAddress, "1"))],
        };
        var genesisBlockA = genesisBlockBuilderA.Create(proposer);
        var genesisBlockBuilderB = TestUtils.GenesisBlockBuilder with
        {
            Actions = [DumbAction.Create((signerAddress, "2"))],
        };
        var genesisBlockB = genesisBlockBuilderB.Create(proposer);
        var genesisBlockC = TestUtils.GenesisBlockBuilder.Create(proposer);

        var blockchainA = new Blockchain(genesisBlockA);
        var blockchainB = new Blockchain(genesisBlockB);
        var blockchainC = new Blockchain(genesisBlockC);

        var transportA = CreateTransport(signerA);
        var transportB = CreateTransport(signerB);
        var transportC = CreateTransport(signerC);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var syncResponderServiceA = new BlockSynchronizationResponderService(blockchainA, transportA);
        var syncServiceB = new BlockSynchronizationService(blockchainB, transportB);
        var syncServiceC = new BlockSynchronizationService(blockchainC, transportC);

        await using var services = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
            syncResponderServiceA,
            syncServiceB,
            syncServiceC,
        };

        await services.StartAsync(cancellationToken);

        peersA.Add(transportB.Peer);
        peersA.Add(transportC.Peer);

        var (block, _) = blockchainA.ProposeAndAppend(signerA);

        InvokeDelay(() => peerExplorerA.BroadcastBlock(blockchainA, block), 100);

        await Task.WhenAll(
            transportB.MessageRouter.MessageHandlingFailed.WaitAsync(
                predicate: e => e.Handler is BlockSummaryMessageHandler),
            syncServiceC.Appended.WaitAsync())
            .WaitAsync(WaitTimeout5, cancellationToken);

        Assert.NotEqual(blockchainA.Genesis, blockchainB.Genesis);
        Assert.Equal(blockchainA.Blocks.Keys, blockchainC.Blocks.Keys);
        Assert.Equal(2, blockchainA.Blocks.Count);
        Assert.Single(blockchainB.Blocks);
        Assert.Equal(2, blockchainC.Blocks.Count);

        Assert.Equal(
            "1",
            blockchainA
                .GetWorld()
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValue(signerAddress));
        Assert.Equal(
            "2",
            blockchainB
                .GetWorld()
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValue(signerAddress));
        Assert.Equal(
            "1",
            blockchainC
                .GetWorld()
                .GetAccount(SystemAddresses.SystemAccount)
                .GetValue(signerAddress));
    }

    [Fact(Timeout = Timeout)]
    public async Task FindSpecificPeerAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var transportC = CreateTransport();
        var transportD = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peersD = new PeerCollection(transportD.Peer.Address);
        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        using var peerExplorerC = new PeerExplorer(transportC, peersC);
        using var peerExplorerD = new PeerExplorer(transportD, peersD);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
            transportD,
        };

        await transports.StartAsync(cancellationToken);

        await peerExplorerA.PingAsync(peerExplorerB.Peer, cancellationToken);
        await peerExplorerB.PingAsync(peerExplorerC.Peer, cancellationToken);
        await peerExplorerC.PingAsync(peerExplorerD.Peer, cancellationToken);

        var foundPeer1 = await peerExplorerA.FindPeerAsync(transportB.Peer.Address, int.MaxValue, cancellationToken);

        Assert.Equal(transportB.Peer.Address, foundPeer1.Address);
        Assert.DoesNotContain(transportC.Peer, peersA);

        var foundPeer2 = await peerExplorerA.FindPeerAsync(transportD.Peer.Address, int.MaxValue, cancellationToken);

        Assert.Equal(transportD.Peer.Address, foundPeer2.Address);
        Assert.Contains(transportC.Peer, peersA);
        Assert.Contains(transportD.Peer, peersA);
    }

    [Fact(Timeout = Timeout)]
    public async Task FindSpecificPeerAsyncFail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var transportC = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var peerExplorerC = new PeerExplorer(transportC, peersC);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
        };

        await transports.StartAsync(cancellationToken);

        await peerExplorerA.PingAsync(peerExplorerB.Peer, cancellationToken);
        await peerExplorerB.PingAsync(peerExplorerC.Peer, cancellationToken);

        await transportB.DisposeAsync();

        await Assert.ThrowsAsync<PeerNotFoundException>(
            () => peerExplorerA.FindPeerAsync(transportB.Peer.Address, int.MaxValue, cancellationToken));
        await Assert.ThrowsAsync<PeerNotFoundException>(
            () => peerExplorerA.FindPeerAsync(transportC.Peer.Address, int.MaxValue, cancellationToken));

        Assert.DoesNotContain(transportC.Peer, peersA);
    }

    [Fact(Timeout = Timeout)]
    public async Task FindSpecificPeerAsyncDepthFail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var transportC = CreateTransport();
        var transportD = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peersD = new PeerCollection(transportD.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var peerExplorerC = new PeerExplorer(transportC, peersC);
        var peerExplorerD = new PeerExplorer(transportD, peersD);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
            transportD,
        };

        await transports.StartAsync(cancellationToken);

        await peerExplorerA.PingAsync(peerExplorerB.Peer, cancellationToken);
        await peerExplorerB.PingAsync(peerExplorerC.Peer, cancellationToken);
        await peerExplorerC.PingAsync(peerExplorerD.Peer, cancellationToken);

        var foundPeer = await peerExplorerA.FindPeerAsync(transportC.Peer.Address, 1, cancellationToken);

        Assert.Equal(transportC.Peer.Address, foundPeer.Address);
        peerExplorerA.Peers.Clear();
        Assert.Empty(peersA);

        await peerExplorerA.PingAsync(peerExplorerB.Peer, cancellationToken);
        await Assert.ThrowsAsync<PeerNotFoundException>(
            async () => await peerExplorerA.FindPeerAsync(transportD.Peer.Address, 1, cancellationToken));
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotFillWhenGetAllBlockAtFirstTimeFromSender()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var blockchainA = new Blockchain(genesisBlock);
        var blockchainB = new Blockchain(genesisBlock);

        var syncServiceA = new BlockSynchronizationService(blockchainA, transportA);
        var syncResponderServiceB = new BlockSynchronizationResponderService(blockchainB, transportB)
        {
            MaxHashesPerResponse = 8,
        };

        await using var services = new ServiceCollection
        {
            transportA,
            transportB,
            syncServiceA,
            syncResponderServiceB,
        };

        await services.StartAsync(cancellationToken);

        blockchainB.ProposeAndAppendMany(proposer, 6);

        await peerExplorerB.PingAsync(peerExplorerA.Peer, cancellationToken);
        InvokeDelay(() => peerExplorerB.BroadcastBlock(blockchainB), 100);
        await syncServiceA.Appended.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken);

        Assert.Equal(7, blockchainA.Blocks.Count);
    }

    [Fact(Timeout = Timeout)]
    public async Task FillWhenGetAChunkOfBlocksFromSender()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchainA = new Blockchain(genesisBlock);
        var blockchainB = new Blockchain(genesisBlock);
        var syncServiceA = new BlockSynchronizationService(blockchainA, transportA);
        var syncResponderServiceB = new BlockSynchronizationResponderService(blockchainB, transportB)
        {
            MaxHashesPerResponse = 2,
        };

        await using var services = new ServiceCollection
        {
            transportA,
            transportB,
            syncServiceA,
            syncResponderServiceB,
        };

        await services.StartAsync(cancellationToken);

        blockchainB.ProposeAndAppendMany(proposer, 6);

        await peerExplorerB.PingAsync(peerExplorerA.Peer, cancellationToken);
        InvokeDelay(() => peerExplorerB.BroadcastBlock(blockchainB), 100);
        await syncServiceA.Appended.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken);

        Assert.Equal(2, blockchainA.Blocks.Count);
    }

    [Fact(Timeout = Timeout)]
    public async Task FillWhenGetAllBlocksFromSender()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchainA = new Blockchain(genesisBlock);
        var blockchainB = new Blockchain(genesisBlock);
        var syncServiceA = new BlockSynchronizationService(blockchainA, transportA);
        var syncResponderServiceB = new BlockSynchronizationResponderService(blockchainB, transportB)
        {
            MaxHashesPerResponse = 3,
        };

        await using var services = new ServiceCollection
        {
            transportA,
            transportB,
            syncServiceA,
            syncResponderServiceB,
        };

        await services.StartAsync(cancellationToken);

        blockchainB.ProposeAndAppendMany(proposer, 6);
        await peerExplorerB.PingAsync(peerExplorerA.Peer, cancellationToken);

        InvokeDelay(() => peerExplorerB.BroadcastBlock(blockchainB), 100);
        await syncServiceA.Appended.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(3, blockchainA.Blocks.Count);

        InvokeDelay(() => peerExplorerB.BroadcastBlock(blockchainB), 100);
        await syncServiceA.Appended.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(5, blockchainA.Blocks.Count);

        InvokeDelay(() => peerExplorerB.BroadcastBlock(blockchainB), 100);
        await syncServiceA.Appended.WaitAsync().WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(7, blockchainA.Blocks.Count);
    }

    [Fact(Timeout = Timeout)]
    public async Task GetPeerChainStateAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var signerB = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);

        var transportA = CreateTransport();
        var transportB = CreateTransport(signerB);
        var transportC = CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        _ = new PeerExplorer(transportB, peersB);
        _ = new PeerExplorer(transportC, peersC);
        var blockchainB = new Blockchain(genesisBlock);
        var blockchainC = new Blockchain(genesisBlock);

        transportB.MessageRouter.Register(
            new BlockchainStateRequestMessageHandler(blockchainB, transportB));
        transportC.MessageRouter.Register(
            new BlockchainStateRequestMessageHandler(blockchainC, transportC));

        var blockchainStates1 = await peerExplorerA.GetBlockchainStateAsync(cancellationToken);
        Assert.Empty(blockchainStates1);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
        };

        await transports.StartAsync(cancellationToken);

        peersA.Add(transportB.Peer);

        var blockchainStates2 = await peerExplorerA.GetBlockchainStateAsync(cancellationToken);
        Assert.Equal(
            new BlockchainState(transportB.Peer, blockchainB.Genesis, blockchainB.Genesis),
            blockchainStates2[0]);

        var block = blockchainB.Propose(signerB);
        blockchainB.Append(block, CreateBlockCommit(block));

        var blockchainStates3 = await peerExplorerA.GetBlockchainStateAsync(cancellationToken);
        Assert.Equal(
            new BlockchainState(transportB.Peer, blockchainB.Genesis, blockchainB.Tip),
            blockchainStates3[0]);

        peersA.Add(transportC.Peer);
        var blockchainStates4 = await peerExplorerA.GetBlockchainStateAsync(cancellationToken);
        Assert.Equal(
            [
                new BlockchainState(transportB.Peer, blockchainB.Genesis, blockchainB.Tip),
                new BlockchainState(transportC.Peer, blockchainC.Genesis, blockchainC.Tip),
            ],
            blockchainStates4.ToHashSet());
    }

    [Fact(Timeout = Timeout)]
    public async Task RegulateGetBlocksMsg()
    {
        const int MaxConcurrentResponses = 3;
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var blockchainA = new Blockchain(genesisBlock);
        var syncResponderServiceA = new BlockSynchronizationResponderService(blockchainA, transportA)
        {
            MaxConcurrentResponses = MaxConcurrentResponses,
        };

        await using var services = new ServiceCollection
        {
            transportA,
            transportB,
            syncResponderServiceA,
        };

        await services.StartAsync(cancellationToken);

        var tasks = new List<Task>();
        BlockHash[] blockHashes = [blockchainA.Genesis.BlockHash];
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(transportB.GetBlocksAsync(transportA.Peer, blockHashes, cancellationToken).ToArrayAsync(cancellationToken).AsTask());
        }

        await TaskUtility.TryWhenAll(tasks);

        var failedTasks = tasks.Where(item => item.IsFaulted);
        var succeededTasks = tasks.Where(item => item.IsCompletedSuccessfully);

        Assert.Equal(MaxConcurrentResponses, succeededTasks.Count());
        Assert.All(failedTasks, task =>
        {
            var e = Assert.IsType<AggregateException>(task.Exception);
            var ie = Assert.Single(e.InnerExceptions);
            Assert.IsType<TimeoutException>(ie);
        });
    }

    [Fact(Timeout = Timeout)]
    public async Task RegulateGetTxsMsg()
    {
        const int MaxConcurrentResponses = 3;
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var transportA = CreateTransport();
        var transportB = CreateTransport();
        var blockchainA = new Blockchain(genesisBlock);
        var txIds = blockchainA.Transactions.Keys.ToArray();

        var syncResponderServiceA = new TransactionSynchronizationResponderService(blockchainA, transportA)
        {
            MaxConcurrentResponses = MaxConcurrentResponses,
        };

        await using var services = new ServiceCollection
        {
            transportA,
            transportB,
            syncResponderServiceA,
        };

        await services.StartAsync(cancellationToken);

        var tasks = new List<Task>();
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(transportB.GetTransactionsAsync(transportA.Peer, txIds, cancellationToken).ToArrayAsync(cancellationToken).AsTask());
        }

        await TaskUtility.TryWhenAll(tasks);

        var failedTasks = tasks.Where(item => item.IsFaulted);
        var succeededTasks = tasks.Where(item => item.IsCompletedSuccessfully);

        Assert.Equal(MaxConcurrentResponses, succeededTasks.Count());
        Assert.All(failedTasks, task =>
        {
            var e = Assert.IsType<AggregateException>(task.Exception);
            var ie = Assert.Single(e.InnerExceptions);
            Assert.IsType<TimeoutException>(ie);
        });
    }
}
