using System.Net;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Nito.AsyncEx;
using Serilog;
using xRetry;
using Xunit.Abstractions;
using Libplanet.Extensions;
using static Libplanet.Tests.TestUtils;
using Libplanet.Types.Threading;
using Libplanet.Net.Services;
using Libplanet.Tests;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Components;
using System.Reactive.Linq;

namespace Libplanet.Net.Tests;

public partial class SwarmTest(ITestOutputHelper output)
{
    private const int Timeout = 60 * 1000;

    [Fact(Timeout = Timeout)]
    public async Task HandleReconnection()
    {
        await using var transport = TestUtils.CreateTransport();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        var peers = new PeerCollection(transport.Peer.Address);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);

        var peerExplorer = new PeerExplorer(transport, peers);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);

        await transport.StartAsync(default);
        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await peerExplorerA.PingAsync(peerExplorer.Peer, default);
        await transportA.StopAsync(default);
        await peerExplorerB.PingAsync(peerExplorer.Peer, default);

        Assert.Contains(transportB.Peer, peerExplorer.Peers);
        Assert.Contains(transport.Peer, peerExplorerB.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task AddPeersWithoutStart()
    {
        await using var a = TestUtils.CreateTransport();
        await using var b = TestUtils.CreateTransport();
        var peersA = new PeerCollection(a.Peer.Address);
        var peersB = new PeerCollection(b.Peer.Address);
        var peerExplorerA = new PeerExplorer(a, peersA);
        var peerExplorerB = new PeerExplorer(b, peersB);

        await b.StartAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => peerExplorerA.PingAsync(peerExplorerB.Peer, default));
    }

    [Fact(Timeout = Timeout)]
    public async Task AddPeersAsync()
    {
        await using var a = TestUtils.CreateTransport();
        await using var b = TestUtils.CreateTransport();
        var peersA = new PeerCollection(a.Peer.Address);
        var peersB = new PeerCollection(b.Peer.Address);
        var peerExplorerA = new PeerExplorer(a, peersA);
        var peerExplorerB = new PeerExplorer(b, peersB);

        await a.StartAsync(default);
        await b.StartAsync(default);

        await peerExplorerA.PingAsync(peerExplorerB.Peer, default);

        Assert.Contains(a.Peer, peerExplorerB.Peers);
        Assert.Contains(b.Peer, peerExplorerA.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task BootstrapException()
    {
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        _ = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB)
        {
            SeedPeers = [transportA.Peer],
        };

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await peerExplorerB.ExploreAsync(default);
        Assert.Empty(peerExplorerB.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task BootstrapAsyncWithoutStart()
    {
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var transportC = TestUtils.CreateTransport();
        await using var transportD = TestUtils.CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peersD = new PeerCollection(transportD.Peer.Address);
        _ = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var peerExplorerC = new PeerExplorer(transportC, peersC);
        _ = new PeerExplorer(transportD, peersD);

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        await transportD.StartAsync(default);

        var bootstrappedAt = DateTimeOffset.UtcNow;
        peerExplorerC.Peers.AddOrUpdate(transportD.Peer);
        await peerExplorerB.PingAsync(transportA.Peer, default);
        await peerExplorerC.PingAsync(transportA.Peer, default);

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
            if (peer.Address == transportD.Peer.Address)
            {
                // Peers added before bootstrap should not be marked as stale.
                Assert.InRange(peerState.LastUpdated, bootstrappedAt, DateTimeOffset.UtcNow);
            }
            else
            {
                Assert.Equal(DateTimeOffset.MinValue, peerState.LastUpdated);
            }
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task MaintainStaticPeers()
    {
        var keyA = new PrivateKey();

        await using var transportA = TestUtils.CreateTransport(keyA);
        await using var transportB = TestUtils.CreateTransport();

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);


        await using var transport = TestUtils.CreateTransport();
        // options: new SwarmOptions
        // {
        //     StaticPeers = new[]
        //     {
        //         transportA.Peer,
        //         transportB.Peer,
        //         // Unreachable peer:
        //         new Peer
        //         {
        //             Address = new PrivateKey().Address,
        //             EndPoint = new DnsEndPoint("127.0.0.1", 65535),
        //         },
        //         }.ToImmutableHashSet(),
        //     StaticPeersMaintainPeriod = TimeSpan.FromMilliseconds(100),
        // });

        var peers = new PeerCollection(transport.Peer.Address);
        var peerExplorer = new PeerExplorer(transport, peers)
        {
            SeedPeers = [transport.Peer],
        };
        var refreshService = new RefreshStaticPeersService(peerExplorer, [transportA.Peer, transportB.Peer]);

        await transport.StartAsync(default);
        await refreshService.StartAsync(default);

        await Task.WhenAll(
            refreshService.PeerAdded.WaitAsync(predicate: p => p == transportA.Peer)
                .WaitAsync(TimeSpan.FromSeconds(5)),
            refreshService.PeerAdded.WaitAsync(predicate: p => p == transportB.Peer)
                .WaitAsync(TimeSpan.FromSeconds(5)));

        await transportA.DisposeAsync();
        await Task.Delay(100);
        await peerExplorer.RefreshAsync(TimeSpan.Zero, default);
        // Invoke once more in case of swarmA and swarmB is in the same bucket,
        // and swarmA is last updated.
        await peerExplorer.RefreshAsync(TimeSpan.Zero, default);
        Assert.DoesNotContain(transportA.Peer, peerExplorer.Peers);
        Assert.Contains(transportB.Peer, peerExplorer.Peers);

        var transportOptionsC = new TransportOptions
        {
            Port = transportA.Peer.EndPoint.Port,
        };
        await using var transportC = TestUtils.CreateTransport(keyA, options: transportOptionsC);
        await transportC.StartAsync(default);

        await Task.WhenAll(
            refreshService.PeerAdded.WaitAsync(predicate: p => p == transportA.Peer)
                .WaitAsync(TimeSpan.FromSeconds(5)),
            refreshService.PeerAdded.WaitAsync(predicate: p => p == transportB.Peer)
                .WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact(Timeout = Timeout)]
    public async Task BootstrapContext()
    {
        var collectedTwoMessages = Enumerable.Range(0, 4).Select(i =>
            new AsyncAutoResetEvent()).ToList();
        var stepChangedToPreCommits = Enumerable.Range(0, 4).Select(i =>
            new AsyncAutoResetEvent()).ToList();
        var roundChangedToOnes = Enumerable.Range(0, 4).Select(i =>
            new AsyncAutoResetEvent()).ToList();
        var roundOneProposed = new AsyncAutoResetEvent();
        var policy = new BlockchainOptions();
        var genesis = new MemoryRepositoryFixture(policy).GenesisBlock;

        var consensusPeers = Enumerable.Range(0, 4).Select(i =>
            new Peer
            {
                Address = TestUtils.PrivateKeys[i].Address,
                EndPoint = new DnsEndPoint("127.0.0.1", 6000 + i),
            }).ToImmutableHashSet();
        var reactorOpts = Enumerable.Range(0, 4).Select(i =>
            new ConsensusServiceOptions
            {
                Validators = consensusPeers,
                // TransportOptions = new TransportOptions
                // {
                //     Port = 6000 + i,
                // },
                Workers = 100,
                TargetBlockInterval = TimeSpan.FromSeconds(10),
                ConsensusOptions = new ConsensusOptions(),
            }).ToList();
        var swarms = new List<Swarm>();
        for (int i = 0; i < 4; i++)
        {
            swarms.Add(await CreateSwarm(
                privateKey: TestUtils.PrivateKeys[i],
                options: new SwarmOptions
                {
                    TransportOptions = new TransportOptions
                    {
                        Host = "127.0.0.1",
                        Port = 9000 + i,
                    },
                },
                blockchainOptions: policy,
                genesis: genesis,
                consensusServiceOption: reactorOpts[i]));
        }
        await using var _1 = new AsyncDisposerCollection(swarms);

        // swarms[1] is the round 0 proposer for height 1.
        // swarms[2] is the round 1 proposer for height 2.
        _ = swarms[0].StartAsync(default);
        _ = swarms[3].StartAsync(default);

        // swarms[0].ConsensusReactor.StateChanged += (_, eventArgs) =>
        // {
        //     if (eventArgs.VoteCount == 2)
        //     {
        //         collectedTwoMessages[0].Set();
        //     }
        // };

        // Make sure both swarms time out and swarm[0] collects two PreVotes.
        await collectedTwoMessages[0].WaitAsync();

        // Dispose swarm[3] to simulate shutdown during bootstrap.
        await swarms[3].DisposeAsync();

        // Bring swarm[2] online.
        _ = swarms[2].StartAsync(default);
        // swarms[0].ConsensusReactor.StateChanged += (_, eventArgs) =>
        // {
        //     if (eventArgs.Step == ConsensusStep.PreCommit)
        //     {
        //         stepChangedToPreCommits[0].Set();
        //     }
        // };
        // swarms[2].ConsensusReactor.StateChanged += (_, eventArgs) =>
        // {
        //     if (eventArgs.Step == ConsensusStep.PreCommit)
        //     {
        //         stepChangedToPreCommits[2].Set();
        //     }
        // };

        // Since we already have swarm[3]'s PreVote, when swarm[2] times out,
        // swarm[2] adds additional PreVote, making it possible to reach PreCommit.
        // Current network's context state should be:
        // Proposal: null
        // PreVote: swarm[0], swarm[2], swarm[3],
        // PreCommit: swarm[0], swarm[2]
        await Task.WhenAll(
            stepChangedToPreCommits[0].WaitAsync(), stepChangedToPreCommits[2].WaitAsync());

        // After swarm[1] comes online, eventually it'll catch up to vote PreCommit,
        // at which point the round will move to 1 where swarm[2] is the proposer.
        _ = swarms[1].StartAsync(default);
        // swarms[2].ConsensusReactor.MessagePublished += (_, eventArgs) =>
        // {
        //     if (eventArgs.Message is ConsensusProposalMessage proposalMsg &&
        //         proposalMsg.Round == 1 &&
        //         proposalMsg.Validator.Equals(TestUtils.PrivateKeys[2].PublicKey))
        //     {
        //         roundOneProposed.Set();
        //     }
        // };

        await roundOneProposed.WaitAsync();

        await AssertThatEventually(() => swarms[0].Blockchain.Tip.Height == 1, int.MaxValue);
        Assert.Equal(1, swarms[0].Blockchain.BlockCommits[1].Round);
    }

    [Fact(Timeout = Timeout)]
    public async Task GetBlocks()
    {
        var keyA = new PrivateKey();
        using var fx = new MemoryRepositoryFixture();
        var genesis = fx.GenesisBlock;
        var blockchainA = MakeBlockchain(genesisBlock: genesis);
        var blockchainB = MakeBlockchain(genesisBlock: genesis);

        await using var transportA = TestUtils.CreateTransport(keyA);
        await using var transportB = TestUtils.CreateTransport();
        using var fetcherB = new BlockFetcher(blockchainB, transportB);

        var (block1, blockCommit1) = blockchainA.ProposeAndAppend(keyA);
        var (block2, blockCommit2) = blockchainA.ProposeAndAppend(keyA);

        transportA.MessageRouter.Register(new BlockHashRequestMessageHandler(blockchainA, transportA));
        transportA.MessageRouter.Register(new BlockRequestMessageHandler(blockchainA, transportA, 1));

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);
        var blockHashes = ImmutableArray.CreateRange([block1.BlockHash, block2.BlockHash]);
        var blockPairs = await fetcherB.FetchAsync(transportA.Peer, blockHashes, default);

        Assert.Equal([block1, block2], [.. blockPairs.Select(item => item.Item1)]);
        Assert.Equal([blockCommit1, blockCommit2], blockPairs.Select(item => item.Item2).ToArray());
    }

    [Fact(Timeout = Timeout)]
    public async Task GetMultipleBlocksAtOnce()
    {
        var keyA = new PrivateKey();
        var keyB = new PrivateKey();

        var transportA = TestUtils.CreateTransport(keyA);
        var transportB = TestUtils.CreateTransport(keyB);
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var blockchainA = MakeBlockchain();
        var blockchainB = MakeBlockchain();

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };

        blockchainA.ProposeAndAppend(keyA);
        blockchainA.ProposeAndAppend(keyA);

        transportA.MessageRouter.Register(new BlockHashRequestMessageHandler(blockchainA, transportA));
        transportA.MessageRouter.Register(new BlockRequestMessageHandler(blockchainA, transportA, 2));

        await transports.StartAsync(default);

        peersB.AddOrUpdate(transportA.Peer);

        var hashes = await transportB.GetBlockHashesAsync(
            transportA.Peer,
            blockchainA.Genesis.BlockHash,
            default);

        var request = new BlockRequestMessage { BlockHashes = [.. hashes], ChunkSize = 2 };
        var responses = await transportB.SendAsync<BlockResponseMessage>(
            transportA.Peer, request, response => response.IsLast, default).ToArrayAsync();

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
        var keyB = new PrivateKey();
        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport(keyB);
        var blockchainA = MakeBlockchain();
        var blockchainB = MakeBlockchain();
        var fetcherA = new TransactionFetcher(blockchainA, transportA);
        var txKey = new PrivateKey();
        var tx = new TransactionBuilder
        {
            GenesisHash = blockchainB.Genesis.BlockHash,
        }.Create(txKey);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
        };

        blockchainB.StagedTransactions.Add(tx);
        blockchainB.ProposeAndAppend(keyB);

        transportB.MessageRouter.Register(new TransactionRequestMessageHandler(blockchainB, transportB, 1));

        await transports.StartAsync(default);

        var txs = await fetcherA.FetchAsync(transportB.Peer, [tx.Id], default);
        Assert.Equal(new[] { tx }, txs);
    }

    // [Fact(Timeout = Timeout)]
    // public async Task CanResolveEndPoint()
    // {
    //     var random = RandomUtility.GetRandom(output);
    //     var peer = RandomUtility.LocalPeer(random);
    //     var transportOptions = new TransportOptions
    //     {
    //         Host = peer.EndPoint.Host,
    //         Port = peer.EndPoint.Port,
    //     };
    //     var options = new SwarmOptions
    //     {
    //         TransportOptions = transportOptions,
    //     };
    //     await using var swarm = await CreateSwarm(options: options);
    //     Assert.Equal(peer.EndPoint, swarm.Peer.EndPoint);
    // }

    // [Fact(Timeout = Timeout)]
    // public async Task StopGracefullyWhileStarting()
    // {
    //     await using var a = await CreateSwarm();

    //     Task t = a.StartAsync(default);
    //     bool canceled = false;
    //     try
    //     {
    //         await Task.WhenAll(a.StopAsync(default), t);
    //     }
    //     catch (OperationCanceledException)
    //     {
    //         canceled = true;
    //     }

    //     Assert.True(canceled || t.IsCompleted);
    // }

    // [Fact(Timeout = Timeout)]
    // public async Task AsPeer()
    // {
    //     await using var swarm = await CreateSwarm();
    //     Assert.IsType<Peer>(swarm.Peer);

    //     await swarm.StartAsync(default);
    //     Assert.IsType<Peer>(swarm.Peer);
    // }

    [Fact(Timeout = Timeout)]
    public async Task CannotBlockSyncWithForkedChain()
    {
        var key1 = new PrivateKey();
        var key2 = new PrivateKey();
        var transportA = TestUtils.CreateTransport(key1);
        var transportB = TestUtils.CreateTransport(key2);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var blockchainA = MakeBlockchain();
        var blockchainB = MakeBlockchain();
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

        var privateKey = new PrivateKey();
        var addr = transportA.Peer.Address;
        var item = "foo";

        blockchainA.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = [DumbAction.Create((addr, item))],
        });
        var (tipA, _) = blockchainA.ProposeAndAppend(key1);

        blockchainB.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = [DumbAction.Create((addr, item))],
        });
        blockchainB.ProposeAndAppend(key2);

        blockchainB.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = [DumbAction.Create((addr, item))],
        });
        var (tipB, _) = blockchainB.ProposeAndAppend(key2);

        await transports.StartAsync(default);
        await services.StartAsync(default);

        peersB.Add(transportA.Peer);
        peerExplorerB.BroadcastBlock(blockchainB, tipB);

        await syncServiceA.FetchingFailed.WaitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(tipA, blockchainA.Tip);
    }

    [Fact(Timeout = Timeout)]
    public async Task UnstageInvalidTransaction()
    {
        var validKey = new PrivateKey();
        var blockchainOptions = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validators =
                [
                    new RelayObjectValidator<Transaction>(tx =>
                    {
                        var validAddress = validKey.Address;
                        if (!tx.Signer.Equals(validAddress) && !tx.Signer.Equals(GenesisProposer.Address))
                        {
                            throw new InvalidOperationException("invalid signer");
                        }
                    }),
                ],
            },
        };
        using var fx1 = new MemoryRepositoryFixture();
        using var fx2 = new MemoryRepositoryFixture();

        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var blockchainA = MakeBlockchain(blockchainOptions, privateKey: validKey);
        var blockchainB = MakeBlockchain(blockchainOptions, privateKey: validKey);

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

        var invalidKey = new PrivateKey();
        var validTx = blockchainA.StagedTransactions.Add(validKey);
        var invalidTx = new TransactionBuilder
        {
            GenesisHash = blockchainB.Genesis.BlockHash,
        }.Create(invalidKey);
        Assert.Throws<InvalidOperationException>(() => blockchainA.StagedTransactions.Add(invalidKey));

        await transports.StartAsync(default);
        await services.StartAsync(default);

        peersA.Add(transportB.Peer);

        peerExplorerA.BroadcastTransaction([validTx, invalidTx]);
        await syncServiceB.Staged.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(blockchainB.StagedTransactions[validTx.Id], validTx);
        Assert.Throws<KeyNotFoundException>(
            () => blockchainB.Transactions[invalidTx.Id]);

        Assert.Contains(validTx.Id, blockchainB.StagedTransactions.Keys);
        Assert.DoesNotContain(invalidTx.Id, blockchainB.StagedTransactions.Keys);
    }

    [RetryFact(Timeout = Timeout)]
    public async Task IgnoreTransactionFromDifferentGenesis()
    {
        var validKey = new PrivateKey();
        var blockchainOptions = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validators =
                [
                    new RelayObjectValidator<Transaction>(tx =>
                    {
                        var validAddress = validKey.Address;
                        if (!tx.Signer.Equals(validAddress) && !tx.Signer.Equals(GenesisProposer.Address))
                        {
                            throw new InvalidOperationException("invalid signer");
                        }
                    }),
                ],
            },
        };
        using var fx1 = new MemoryRepositoryFixture();
        using var fx2 = new MemoryRepositoryFixture();
        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        using var peerExplorerA = new PeerExplorer(transportA, peersA);

        var genesisBlockA = new BlockBuilder { }.Create(validKey);
        var genesisBlockB = new BlockBuilder { }.Create(validKey);
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

        var tx = blockchainA.StagedTransactions.Add(validKey);

        await services.StartAsync(default);
        peersA.Add(transportB.Peer);

        peerExplorerA.BroadcastTransaction([tx]);
        await syncServiceB.StageFailed.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Throws<KeyNotFoundException>(() => blockchainB.Transactions[tx.Id]);
        Assert.DoesNotContain(tx.Id, blockchainB.StagedTransactions.Keys);
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotReceiveBlockFromNodeHavingDifferentGenesisBlock()
    {
        var privateKeyA = PrivateKey.Parse("8568eb6f287afedece2c7b918471183db0451e1a61535bb0381cfdf95b85df20");
        var privateKeyB = PrivateKey.Parse("c34f7498befcc39a14f03b37833f6c7bb78310f1243616524eda70e078b8313c");
        var privateKeyC = PrivateKey.Parse("941bc2edfab840d79914d80fe3b30840628ac37a5d812d7f922b5d2405a223d3");

        var signerAddress = new PrivateKey().Address;

        var actionsA = new[] { DumbAction.Create((signerAddress, "1")) };
        var actionsB = new[] { DumbAction.Create((signerAddress, "2")) };

        var blockchainA = MakeBlockchain(new BlockchainOptions(), actionsA, null, privateKeyA);
        var genesisBlockA = blockchainA.Genesis;
        var blockchainB = MakeBlockchain(new BlockchainOptions(), actionsB, null, privateKeyB);
        var blockchainC = MakeBlockchain(new BlockchainOptions(), genesisBlock: genesisBlockA);

        var transportA = TestUtils.CreateTransport(privateKeyA);
        var transportB = TestUtils.CreateTransport(privateKeyB);
        var transportC = TestUtils.CreateTransport(privateKeyC);
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

        await services.StartAsync(default);

        peersA.Add(transportB.Peer);
        peersA.Add(transportC.Peer);

        var (block, _) = blockchainA.ProposeAndAppend(privateKeyA);

        peerExplorerA.BroadcastBlock(blockchainA, block, new BroadcastOptions
        {
            Delay = TimeSpan.FromMilliseconds(100),
        });

        await Task.WhenAll(
            transportB.MessageRouter.ErrorOccurred.WaitAsync(
                predicate: e => e.MessageHandler is BlockSummaryMessageHandler).WaitAsync(TimeSpan.FromSeconds(5)),
            syncServiceC.Appended.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5)));

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
        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var transportC = TestUtils.CreateTransport();
        var transportD = TestUtils.CreateTransport();
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

        await transports.StartAsync(default);

        await peerExplorerA.PingAsync(peerExplorerB.Peer, default);
        await peerExplorerB.PingAsync(peerExplorerC.Peer, default);
        await peerExplorerC.PingAsync(peerExplorerD.Peer, default);

        var foundPeer1 = await peerExplorerA.FindPeerAsync(transportB.Peer.Address, int.MaxValue, default);

        Assert.Equal(transportB.Peer.Address, foundPeer1.Address);
        Assert.DoesNotContain(transportC.Peer, peersA);

        var foundPeer2 = await peerExplorerA.FindPeerAsync(transportD.Peer.Address, int.MaxValue, default);

        Assert.Equal(transportD.Peer.Address, foundPeer2.Address);
        Assert.Contains(transportC.Peer, peersA);
        Assert.Contains(transportD.Peer, peersA);
    }

    [Fact(Timeout = Timeout)]
    public async Task FindSpecificPeerAsyncFail()
    {
        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var transportC = TestUtils.CreateTransport();
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

        await transports.StartAsync(default);

        await peerExplorerA.PingAsync(peerExplorerB.Peer, default);
        await peerExplorerB.PingAsync(peerExplorerC.Peer, default);

        await transportB.DisposeAsync();

        await Assert.ThrowsAsync<PeerNotFoundException>(
            async () => await peerExplorerA.FindPeerAsync(transportB.Peer.Address, int.MaxValue, default));
        await Assert.ThrowsAsync<PeerNotFoundException>(
            async () => await peerExplorerA.FindPeerAsync(transportC.Peer.Address, int.MaxValue, default));

        Assert.DoesNotContain(transportC.Peer, peersA);
    }

    [Fact(Timeout = Timeout)]
    public async Task FindSpecificPeerAsyncDepthFail()
    {
        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var transportC = TestUtils.CreateTransport();
        var transportD = TestUtils.CreateTransport();
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

        await transports.StartAsync(default);

        await peerExplorerA.PingAsync(peerExplorerB.Peer, default);
        await peerExplorerB.PingAsync(peerExplorerC.Peer, default);
        await peerExplorerC.PingAsync(peerExplorerD.Peer, default);

        var foundPeer = await peerExplorerA.FindPeerAsync(transportC.Peer.Address, 1, default);

        Assert.Equal(transportC.Peer.Address, foundPeer.Address);
        peerExplorerA.Peers.Clear();
        Assert.Empty(peersA);

        await peerExplorerA.PingAsync(peerExplorerB.Peer, default);
        await Assert.ThrowsAsync<PeerNotFoundException>(
            async () => await peerExplorerA.FindPeerAsync(transportD.Peer.Address, 1, default));
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotFillWhenGetAllBlockAtFirstTimeFromSender()
    {
        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var blockchainA = MakeBlockchain();
        var blockchainB = MakeBlockchain();

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

        await services.StartAsync(default);

        blockchainB.ProposeAndAppendMany(GenesisProposer, 6);

        await peerExplorerB.PingAsync(peerExplorerA.Peer, default);
        peerExplorerB.BroadcastBlock(blockchainB, new BroadcastOptions
        {
            Delay = TimeSpan.FromMilliseconds(100),
        });
        await syncServiceA.Appended.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(7, blockchainA.Blocks.Count);
    }

    [Fact(Timeout = Timeout)]
    public async Task FillWhenGetAChunkOfBlocksFromSender()
    {
        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        using var peerExplorerA = new PeerExplorer(transportA, peersA);
        using var peerExplorerB = new PeerExplorer(transportB, peersB);
        var blockchainA = MakeBlockchain();
        var blockchainB = MakeBlockchain();
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

        await services.StartAsync(default);

        blockchainB.ProposeAndAppendMany(GenesisProposer, 6);

        await peerExplorerB.PingAsync(peerExplorerA.Peer, default);
        peerExplorerB.BroadcastBlock(blockchainB, new BroadcastOptions
        {
            Delay = TimeSpan.FromMilliseconds(100),
        });
        await syncServiceA.Appended.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, blockchainA.Blocks.Count);
    }

    [Fact(Timeout = Timeout)]
    public async Task FillWhenGetAllBlocksFromSender()
    {
        Swarm receiver = await CreateSwarm();
        Swarm sender = await CreateSwarm();
        await receiver.StartAsync(default);
        await sender.StartAsync(default);

        receiver.FindNextHashesChunkSize = 3;
        sender.FindNextHashesChunkSize = 3;
        Blockchain chain = sender.Blockchain;

        for (int i = 0; i < 6; i++)
        {
            Block block = chain.ProposeBlock(GenesisProposer);
            chain.Append(block, TestUtils.CreateBlockCommit(block));
        }

        Log.Debug("Sender's BlockChain Tip index: #{index}", sender.Blockchain.Tip.Height);

        await sender.AddPeersAsync([receiver.Peer], default);

        sender.BroadcastBlock(sender.Blockchain.Tip);

        // await receiver.BlockReceived.WaitAsync(default);
        // await receiver.BlockAppended.WaitAsync(default);
        Log.Debug("Count: {Count}", receiver.Blockchain.Blocks.Count);
        sender.BroadcastBlock(sender.Blockchain.Tip);
        Assert.Equal(
            3,
            receiver.Blockchain.Blocks.Count);

        sender.BroadcastBlock(sender.Blockchain.Tip);

        // await receiver.BlockReceived.WaitAsync(default);
        // await receiver.BlockAppended.WaitAsync(default);
        Log.Debug("Count: {Count}", receiver.Blockchain.Blocks.Count);
        sender.BroadcastBlock(sender.Blockchain.Tip);
        Assert.Equal(
            5,
            receiver.Blockchain.Blocks.Count);

        sender.BroadcastBlock(sender.Blockchain.Tip);

        // await receiver.BlockReceived.WaitAsync(default);
        // await receiver.BlockAppended.WaitAsync(default);
        Log.Debug("Count: {Count}", receiver.Blockchain.Blocks.Count);
        sender.BroadcastBlock(sender.Blockchain.Tip);
        Assert.Equal(
            7,
            receiver.Blockchain.Blocks.Count);
    }

    [RetryFact(10, Timeout = Timeout)]
    public async Task GetPeerChainStateAsync()
    {
        var key2 = new PrivateKey();

        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport(key2);
        var transportC = TestUtils.CreateTransport();
        var peersA = new PeerCollection(transportA.Peer.Address);
        var peersB = new PeerCollection(transportB.Peer.Address);
        var peersC = new PeerCollection(transportC.Peer.Address);
        var peerExplorerA = new PeerExplorer(transportA, peersA);
        var peerExplorerB = new PeerExplorer(transportB, peersB);
        var peerExplorerC = new PeerExplorer(transportC, peersC);
        var blockchainB = MakeBlockchain();
        var blockchainC = MakeBlockchain();

        transportB.MessageRouter.Register(
            new BlockchainStateRequestMessageHandler(blockchainB, transportB));
        transportC.MessageRouter.Register(
            new BlockchainStateRequestMessageHandler(blockchainC, transportC));

        var blockchainStates1 = await peerExplorerA.GetBlockchainStateAsync(default);
        Assert.Empty(blockchainStates1);

        await using var transports = new ServiceCollection
        {
            transportA,
            transportB,
            transportC,
        };

        await transports.StartAsync(default);

        // await transportA.AddPeersAsync([transportB.Peer], default);
        peersA.Add(transportB.Peer);



        var blockchainStates2 = await peerExplorerA.GetBlockchainStateAsync(default);
        Assert.Equal(
            new BlockchainState(transportB.Peer, blockchainB.Genesis, blockchainB.Genesis),
            blockchainStates2[0]);

        var block = blockchainB.ProposeBlock(key2);
        blockchainB.Append(block, TestUtils.CreateBlockCommit(block));

        var blockchainStates3 = await peerExplorerA.GetBlockchainStateAsync(default);
        Assert.Equal(
            new BlockchainState(transportB.Peer, blockchainB.Genesis, blockchainB.Tip),
            blockchainStates3[0]);

        // await transportA.AddPeersAsync([transportC.Peer], default);
        peersA.Add(transportC.Peer);
        var blockchainStates4 = await peerExplorerA.GetBlockchainStateAsync(default);
        Assert.Equal(
            [
                new BlockchainState(transportB.Peer, blockchainB.Genesis, blockchainB.Tip),
                new BlockchainState(transportC.Peer, blockchainC.Genesis, blockchainC.Tip),
            ],
            blockchainStates4.ToHashSet());
    }

    [RetryFact(10, Timeout = Timeout)]
    public async Task RegulateGetBlocksMsg()
    {
        var options = new SwarmOptions
        {
            TaskRegulationOptions =
            {
                MaxTransferBlocksTaskCount = 3,
            },
            TransportOptions = new TransportOptions
            {
                Host = "localhost",
            },
        };

        await using var swarm = await CreateSwarm(options: options);
        var transport = swarm.Transport;

        await swarm.StartAsync(default);
        var tasks = new List<Task>();
        BlockHash[] blockHashes = [swarm.Blockchain.Genesis.BlockHash];
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(transport.GetBlocksAsync(swarm.Peer, blockHashes, default).ToArrayAsync(default).AsTask());
        }

        await TaskUtility.TryWhenAll(tasks);

        var failedTasks = tasks.Where(item => item.IsFaulted);
        var succeededTasks = tasks.Where(item => item.IsCompletedSuccessfully);

        Assert.Equal(
            options.TaskRegulationOptions.MaxTransferBlocksTaskCount,
            succeededTasks.Count());
        Assert.All(failedTasks, task =>
        {
            var e = Assert.IsType<AggregateException>(task.Exception);
            var ie = Assert.Single(e.InnerExceptions);
            Assert.IsType<TimeoutException>(ie);
        });
    }

    [RetryFact(10, Timeout = Timeout)]
    public async Task RegulateGetTxsMsg()
    {
        var options = new SwarmOptions
        {
            TaskRegulationOptions =
            {
                MaxTransferTxsTaskCount = 3,
            },
            TransportOptions = new TransportOptions
            {
                Host = "localhost",
            },
        };

        await using var swarm = await CreateSwarm(options: options);
        var transport = swarm.Transport;
        var blockchain = swarm.Blockchain;
        var txIds = blockchain.Transactions.Keys.ToArray();

        await swarm.StartAsync(default);

        var tasks = new List<Task>();
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(transport.GetTransactionsAsync(swarm.Peer, txIds, default).ToArrayAsync(default).AsTask());
        }

        await TaskUtility.TryWhenAll(tasks);

        var failedTasks = tasks.Where(item => item.IsFaulted);
        var succeededTasks = tasks.Where(item => item.IsCompletedSuccessfully);

        Assert.Equal(
            options.TaskRegulationOptions.MaxTransferBlocksTaskCount,
            succeededTasks.Count());
        Assert.All(failedTasks, task =>
        {
            var e = Assert.IsType<AggregateException>(task.Exception);
            var ie = Assert.Single(e.InnerExceptions);
            Assert.IsType<TimeoutException>(ie);
        });
    }
}
