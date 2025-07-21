using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Transports;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Nito.AsyncEx;
using Serilog;
using xRetry;
using Xunit.Abstractions;
using Libplanet.Extensions;
using static Libplanet.Tests.TestUtils;
using Libplanet.TestUtilities;

namespace Libplanet.Net.Tests;

public partial class SwarmTest(ITestOutputHelper output)
{
    private const int Timeout = 60 * 1000;

    [Fact(Timeout = Timeout)]
    public async Task CanNotStartTwice()
    {
        await using var swarm = await CreateSwarm();
        await swarm.StartAsync(default);
        Assert.True(swarm.IsRunning);
        await Assert.ThrowsAsync<InvalidOperationException>(() => swarm.StartAsync(default));
    }

    [Fact(Timeout = Timeout)]
    public async Task HandleReconnection()
    {
        await using var seed = await CreateSwarm();
        await using var swarmA = await CreateSwarm();
        await using var swarmB = await CreateSwarm();

        await seed.StartAsync(default);
        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);
        await swarmA.AddPeersAsync([seed.Peer], default);
        await swarmA.StopAsync(default);
        await swarmB.AddPeersAsync([seed.Peer], default);

        Assert.Contains(swarmB.Peer, seed.Peers);
        Assert.Contains(seed.Peer, swarmB.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task RunConsensusReactorIfOptionGiven()
    {
        await using var swarmA = await CreateSwarm();
        await using var swarmB = await CreateConsensusSwarm();

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);
        await Task.Delay(1000);

        Assert.True(swarmA.IsRunning);
        Assert.True(swarmB.IsRunning);
        Assert.False(swarmA.ConsensusRunning);
        Assert.True(swarmB.ConsensusRunning);
    }

    [Fact(Timeout = Timeout)]
    public async Task StopAsyncTest()
    {
        await using var swarm = await CreateSwarm();

        await swarm.StartAsync(default);
        Assert.True(swarm.IsRunning);
        await swarm.StopAsync(default);
        Assert.False(swarm.IsRunning);

        await Assert.ThrowsAsync<InvalidOperationException>(() => swarm.StopAsync(default));
    }

    [Fact(Timeout = Timeout)]
    public async Task AddPeersWithoutStart()
    {
        await using var a = await CreateSwarm();
        await using var b = await CreateSwarm();

        await b.StartAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => a.AddPeersAsync([b.Peer], default));
    }

    [Fact(Timeout = Timeout)]
    public async Task AddPeersAsync()
    {
        await using var a = await CreateSwarm();
        await using var b = await CreateSwarm();

        await a.StartAsync(default);
        await b.StartAsync(default);

        await a.AddPeersAsync([b.Peer], default);

        Assert.Contains(a.Peer, b.Peers);
        Assert.Contains(b.Peer, a.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task BootstrapException()
    {
        await using var swarmA = await CreateSwarm();
        var swarmOptionsB = new SwarmOptions
        {
            BootstrapOptions = new BootstrapOptions
            {
                SeedPeers = [swarmA.Peer],
            },
        };
        await using var swarmB = await CreateSwarm(options: swarmOptionsB);

        await Assert.ThrowsAsync<InvalidOperationException>(() => swarmB.StartAsync(default));
        await swarmA.StartAsync(default);
    }

    [Fact(Timeout = Timeout)]
    public async Task BootstrapAsyncWithoutStart()
    {
        await using var swarmA = await CreateSwarm();
        await using var swarmB = await CreateSwarm();
        await using var swarmC = await CreateSwarm();
        await using var swarmD = await CreateSwarm();

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);
        await swarmD.StartAsync(default);

        var bootstrappedAt = DateTimeOffset.UtcNow;
        swarmC.RoutingTable.AddOrUpdate(swarmD.Peer);
        await BootstrapAsync(swarmB, swarmA.Peer);
        await BootstrapAsync(swarmC, swarmA.Peer);

        Assert.Contains(swarmB.Peer, swarmC.Peers);
        Assert.Contains(swarmC.Peer, swarmB.Peers);
        foreach (PeerState state in swarmB.RoutingTable)
        {
            Assert.InRange(state.LastUpdated, bootstrappedAt, DateTimeOffset.UtcNow);
        }

        foreach (PeerState state in swarmC.RoutingTable)
        {
            if (state.Peer.Address == swarmD.Peer.Address)
            {
                // Peers added before bootstrap should not be marked as stale.
                Assert.InRange(state.LastUpdated, bootstrappedAt, DateTimeOffset.UtcNow);
            }
            else
            {
                Assert.Equal(DateTimeOffset.MinValue, state.LastUpdated);
            }
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task MaintainStaticPeers()
    {
        var keyA = new PrivateKey();
        var hostOptionsA = new TransportOptions { Host = IPAddress.Loopback.ToString(), Port = 20_000 };
        var hostOptionsB = new TransportOptions { Host = IPAddress.Loopback.ToString(), Port = 20_001 };

        await using var swarmA = await CreateSwarm(keyA);
        await using var swarmB = await CreateSwarm();
        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);

        Swarm swarm = await CreateSwarm(
            options: new SwarmOptions
            {
                StaticPeers = new[]
                {
                    swarmA.Peer,
                    swarmB.Peer,
                    // Unreachable peer:
                    new Peer
                    {
                        Address = new PrivateKey().Address,
                        EndPoint = new DnsEndPoint("127.0.0.1", 65535),
                    },
                    }.ToImmutableHashSet(),
                StaticPeersMaintainPeriod = TimeSpan.FromMilliseconds(100),
            });

        await swarm.StartAsync(default);
        await AssertThatEventually(() => swarm.Peers.Contains(swarmA.Peer), 5_000);
        await AssertThatEventually(() => swarm.Peers.Contains(swarmB.Peer), 5_000);

        await swarmA.DisposeAsync();
        await Task.Delay(100);
        await swarm.PeerDiscovery.RefreshPeersAsync(
            TimeSpan.Zero,
            default);
        // Invoke once more in case of swarmA and swarmB is in the same bucket,
        // and swarmA is last updated.
        await swarm.PeerDiscovery.RefreshPeersAsync(
            TimeSpan.Zero,
            default);
        Assert.DoesNotContain(swarmA.Peer, swarm.Peers);
        Assert.Contains(swarmB.Peer, swarm.Peers);

        Swarm swarmC =
            await CreateSwarm(keyA, options: new SwarmOptions { TransportOptions = hostOptionsA });
        await swarmC.StartAsync(default);
        await AssertThatEventually(() => swarm.Peers.Contains(swarmB.Peer), 5_000);
        await AssertThatEventually(() => swarm.Peers.Contains(swarmC.Peer), 5_000);
    }

    [Fact(Timeout = Timeout)]
    public async Task Cancel()
    {
        await using var swarm = await CreateSwarm();
        using var cancellationTokenSource = new CancellationTokenSource(1);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => swarm.StartAsync(cancellationTokenSource.Token));
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
            new ConsensusReactorOptions
            {
                Validators = consensusPeers,
                TransportOptions = new TransportOptions
                {
                    Port = 6000 + i,
                },
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
                policy: policy,
                genesis: genesis,
                consensusReactorOption: reactorOpts[i]));
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
        await using var swarmA = await CreateSwarm(keyA);
        var genesis = swarmA.Blockchain.Genesis;
        await using var swarmB = await CreateSwarm(genesis: genesis);

        var blockchainA = swarmA.Blockchain;
        var block1 = blockchainA.ProposeBlock(keyA);
        blockchainA.Append(block1, TestUtils.CreateBlockCommit(block1));
        var block2 = blockchainA.ProposeBlock(keyA);
        blockchainA.Append(block2, TestUtils.CreateBlockCommit(block2));

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);

        await swarmA.AddPeersAsync([swarmB.Peer], default);

        var inventories = await swarmB.Transport.GetBlockHashesAsync(
            swarmA.Peer,
            genesis.BlockHash,
            default);
        Assert.Equal(
            new[] { genesis.BlockHash, block1.BlockHash, block2.BlockHash },
            inventories);

        (Block, BlockCommit)[] receivedBlocks =
            await swarmB.Transport.GetBlocksAsync(
                swarmA.Peer,
                inventories,
                cancellationToken: default)
            .ToArrayAsync();
        Assert.Equal(
            [genesis, block1, block2], receivedBlocks.Select(pair => pair.Item1));
    }

    [Fact(Timeout = Timeout)]
    public async Task GetMultipleBlocksAtOnce()
    {
        var keyA = new PrivateKey();
        var keyB = new PrivateKey();

        await using var swarmA = await CreateSwarm(keyA);
        Block genesis = swarmA.Blockchain.Genesis;
        await using var swarmB = await CreateSwarm(keyB, genesis: genesis);

        Blockchain chainA = swarmA.Blockchain;
        Blockchain chainB = swarmB.Blockchain;

        Block block1 = chainA.ProposeBlock(keyA);
        chainA.Append(block1, TestUtils.CreateBlockCommit(block1));
        Block block2 = chainA.ProposeBlock(keyA);
        chainA.Append(block2, TestUtils.CreateBlockCommit(block2));

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);

        var peer = swarmA.Peer;

        await swarmB.AddPeersAsync([peer], default);

        var hashes = await swarmB.Transport.GetBlockHashesAsync(
            peer,
            genesis.BlockHash,
            default);

        ITransport transport = swarmB.Transport;

        var request = new GetBlockMessage { BlockHashes = [.. hashes], ChunkSize = 2 };
        transport.Post(
            swarmA.Peer, request);
        // var aggregateMessage = (AggregateMessage)reply;
        // var responses = aggregateMessage.Messages;

        // var blockMessage = (BlockMessage)responses[0];

        // Assert.Equal(2, responses.Length);
        // Assert.Equal(2, blockMessage.Blocks.Length);
        // Assert.Equal(2, blockMessage.BlockCommits.Length);

        // blockMessage = (BlockMessage)responses[1];

        // Assert.Equal(1, blockMessage.Blocks.Length);
    }

    [Fact(Timeout = Timeout)]
    public async Task GetTx()
    {
        var keyB = new PrivateKey();

        await using var swarmA = await CreateSwarm();
        Block genesis = swarmA.Blockchain.Genesis;
        await using var swarmB = await CreateSwarm(keyB, genesis: genesis);
        Blockchain chainB = swarmB.Blockchain;

        var txKey = new PrivateKey();
        Transaction tx = new TransactionMetadata
        {
            Nonce = 0,
            Signer = txKey.Address,
            GenesisHash = chainB.Genesis.BlockHash,
            Actions = Array.Empty<DumbAction>().ToBytecodes(),
        }.Sign(txKey);
        chainB.StagedTransactions.Add(tx);
        Block block = chainB.ProposeBlock(keyB);
        chainB.Append(block, TestUtils.CreateBlockCommit(block));

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);

        await swarmA.AddPeersAsync([swarmB.Peer], default);

        // List<Transaction> txs =
        //     await swarmA.GetTxsAsync(
        //         swarmB.AsPeer,
        //         new[] { tx.Id },
        //         cancellationToken: default)
        //     .ToListAsync();
        var fetcher = new TxFetcher(swarmA.Blockchain, swarmA.Transport, swarmA.Options.TimeoutOptions);
        var txs = await fetcher.FetchAsync(swarmB.Peer, [tx.Id], default).ToArrayAsync(default);

        Assert.Equal(new[] { tx }, txs);
    }

    [Fact(Timeout = Timeout)]
    public async Task ThrowArgumentExceptionInConstructor()
    {
        var fx = new MemoryRepositoryFixture();
        var policy = new BlockchainOptions();
        var blockchain = MakeBlockChain(policy);
        var privateKey = new PrivateKey();
        var protocol = new ProtocolBuilder { Version = 1 }.Create(privateKey);
        var transportOptions = new TransportOptions
        {
            Protocol = protocol,
            Host = IPAddress.Loopback.ToString(),
        };
        var transport = new NetMQTransport(privateKey.AsSigner(), transportOptions);
    }

    [Fact(Timeout = Timeout)]
    public async Task CanResolveEndPoint()
    {
        var expected = new DnsEndPoint("1.2.3.4", 5678);
        var hostOptions = new TransportOptions
        {
            Host = "1.2.3.4",
            Port = 5678,
        };
        await using var s = await CreateSwarm(options: new SwarmOptions { TransportOptions = hostOptions });
        Assert.Equal(expected, s.Peer.EndPoint);
        Assert.Equal(expected, s.Peer.EndPoint);
    }

    [Fact(Timeout = Timeout)]
    public async Task StopGracefullyWhileStarting()
    {
        await using var a = await CreateSwarm();

        Task t = a.StartAsync(default);
        bool canceled = false;
        try
        {
            await Task.WhenAll(a.StopAsync(default), t);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        Assert.True(canceled || t.IsCompleted);
    }

    [Fact(Timeout = Timeout)]
    public async Task AsPeer()
    {
        await using var swarm = await CreateSwarm();
        Assert.IsType<Peer>(swarm.Peer);

        await swarm.StartAsync(default);
        Assert.IsType<Peer>(swarm.Peer);
    }

    // [FactOnlyTurnAvailable(Timeout = Timeout)]
    // public async Task ExchangeWithIceServer()
    // {
    //     var iceServers = FactOnlyTurnAvailableAttribute.GetIceServers();
    //     var seedHostOptions = new TransportOptions
    //     {
    //         Host = "127.0.0.1",
    //     };
    //     var swarmHostOptions = new TransportOptions
    //     {
    //         Host = string.Empty,
    //     };
    //     var seed = await CreateSwarm(transportOptions: seedHostOptions).ConfigureAwait(false);
    //     var swarmA = await CreateSwarm(transportOptions: swarmHostOptions).ConfigureAwait(false);
    //     var swarmB = await CreateSwarm(transportOptions: swarmHostOptions).ConfigureAwait(false);

    //     try
    //     {
    //         await StartAsync(seed);
    //         await swarmA.StartAsync(default);
    //         await swarmB.StartAsync(default);

    //         await swarmA.AddPeersAsync(new[] { seed.AsPeer }, null);
    //         await swarmB.AddPeersAsync(new[] { seed.AsPeer }, null);
    //         await swarmA.AddPeersAsync(new[] { swarmB.AsPeer }, null);

    //         Assert.Equal(
    //             new HashSet<Peer>
    //             {
    //                 swarmA.AsPeer,
    //                 swarmB.AsPeer,
    //             },
    //             seed.Peers.ToHashSet());
    //         Assert.Equal(
    //             new HashSet<Peer> { seed.AsPeer, swarmB.AsPeer },
    //             swarmA.Peers.ToHashSet());
    //         Assert.Equal(
    //             new HashSet<Peer> { seed.AsPeer, swarmA.AsPeer },
    //             swarmB.Peers.ToHashSet());
    //     }
    //     finally
    //     {
    //         CleaningSwarm(seed);
    //         CleaningSwarm(swarmA);
    //         CleaningSwarm(swarmB);
    //     }
    // }

    // [FactOnlyTurnAvailable(10, 5000, Timeout = Timeout)]
    // public async Task ReconnectToTurn()
    // {
    //     int port;
    //     using (var socket = new Socket(SocketType.Stream, ProtocolType.Tcp))
    //     {
    //         socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    //         port = ((IPEndPoint)socket.LocalEndPoint).Port;
    //     }

    //     Uri turnUrl = FactOnlyTurnAvailableAttribute.GetTurnUri();
    //     string[] userInfo = turnUrl.UserInfo.Split(':');
    //     string username = userInfo[0];
    //     string password = userInfo[1];
    //     var proxyUri = new Uri($"turn://{username}:{password}@127.0.0.1:{port}/");
    //     IEnumerable<IceServer> iceServers = new[] { new IceServer(url: proxyUri) };

    //     var cts = new CancellationTokenSource();
    //     var proxyTask = TurnProxy(port, turnUrl, cts.Token);

    //     var seedKey = new PrivateKey();
    //     var seedHostOptions = new TransportOptions
    //     {
    //         Host = "127.0.0.1",
    //     };
    //     var swarmHostOptions = new TransportOptions { };
    //     var seed =
    //         await CreateSwarm(seedKey, transportOptions: seedHostOptions).ConfigureAwait(false);
    //     var swarmA =
    //         await CreateSwarm(transportOptions: swarmHostOptions).ConfigureAwait(false);

    //     async Task RefreshTableAsync(CancellationToken cancellationToken)
    //     {
    //         while (!cancellationToken.IsCancellationRequested)
    //         {
    //             await Task.Delay(1000, cancellationToken);
    //             try
    //             {
    //                 await swarmA.PeerDiscovery.RefreshTableAsync(
    //                     TimeSpan.FromSeconds(1), cancellationToken);
    //             }
    //             catch (InvalidOperationException)
    //             {
    //             }
    //         }
    //     }

    //     async Task MineAndBroadcast(CancellationToken cancellationToken)
    //     {
    //         while (!cancellationToken.IsCancellationRequested)
    //         {
    //             var block = seed.Blockchain.ProposeBlock(seedKey);
    //             seed.Blockchain.Append(block, TestUtils.CreateBlockCommit(block));
    //             seed.BroadcastBlock(block);
    //             await Task.Delay(1000, cancellationToken);
    //         }
    //     }

    //     try
    //     {
    //         await StartAsync(seed);
    //         await swarmA.StartAsync(default, cancellationToken: cts.Token);

    //         await swarmA.AddPeersAsync(new[] { seed.AsPeer }, null);

    //         cts.Cancel();
    //         await proxyTask;
    //         cts = new CancellationTokenSource();

    //         proxyTask = TurnProxy(port, turnUrl, cts.Token);
    //         _ = RefreshTableAsync(cts.Token);
    //         _ = MineAndBroadcast(cts.Token);

    //         cts.CancelAfter(1500);
    //         await swarmA.BlockReceived.WaitAsync(cts.Token);
    //         cts.Cancel();
    //         await Task.Delay(1000);

    //         Assert.NotEqual(swarmA.Blockchain.Genesis, swarmA.Blockchain.Tip);
    //         Assert.Contains(
    //             swarmA.Blockchain.Tip.BlockHash,
    //             seed.Blockchain.Blocks.Keys);
    //     }
    //     finally
    //     {
    //         CleaningSwarm(seed);
    //         CleaningSwarm(swarmA);
    //     }
    // }

    [Fact(Timeout = Timeout)]
    public async Task CannotBlockSyncWithForkedChain()
    {
        var policy = new BlockchainOptions();
        var chain1 = MakeBlockChain(policy);
        var chain2 = MakeBlockChain(policy);

        var key1 = new PrivateKey();
        var key2 = new PrivateKey();

        await using var miner1 = await CreateSwarm(chain1, key1);
        await using var miner2 = await CreateSwarm(chain2, key2);

        var privKey = new PrivateKey();
        var addr = miner1.Address;
        var item = "foo";

        miner1.Blockchain.StagedTransactions.Add(privKey, submission: new()
        {
            Actions = [DumbAction.Create((addr, item))],
        });
        Block block1 = miner1.Blockchain.ProposeBlock(key1);
        miner1.Blockchain.Append(block1, TestUtils.CreateBlockCommit(block1));
        var miner1TipHash = miner1.Blockchain.Tip.BlockHash;

        miner2.Blockchain.StagedTransactions.Add(privKey, submission: new()
        {
            Actions = [DumbAction.Create((addr, item))],
        });
        Block block2 = miner2.Blockchain.ProposeBlock(key2);
        miner2.Blockchain.Append(block2, TestUtils.CreateBlockCommit(block2));

        miner2.Blockchain.StagedTransactions.Add(privKey, submission: new()
        {
            Actions = [DumbAction.Create((addr, item))],
        });
        var latest = miner2.Blockchain.ProposeBlock(key2);
        miner2.Blockchain.Append(latest, TestUtils.CreateBlockCommit(latest));

        await miner1.StartAsync(default);
        await miner2.StartAsync(default);

        await BootstrapAsync(miner2, miner1.Peer);

        miner2.BroadcastBlock(latest);

        await Task.Delay(5_000);
        Assert.Equal(miner1TipHash, miner1.Blockchain.Tip.BlockHash);
    }

    [Fact(Timeout = Timeout)]
    public async Task UnstageInvalidTransaction()
    {
        var validKey = new PrivateKey();

        void IsSignerValid(Transaction tx)
        {
            var validAddress = validKey.Address;
            if (!tx.Signer.Equals(validAddress) && !tx.Signer.Equals(GenesisProposer.Address))
            {
                throw new InvalidOperationException("invalid signer");
            }
        }

        var blockchainOptions = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validator = new RelayValidator<Transaction>(IsSignerValid),
            },
        };
        using var fx1 = new MemoryRepositoryFixture();
        using var fx2 = new MemoryRepositoryFixture();

        await using var swarmA = await CreateSwarm(
            MakeBlockChain(blockchainOptions, privateKey: validKey));
        await using var swarmB = await CreateSwarm(
            MakeBlockChain(blockchainOptions, privateKey: validKey));

        var invalidKey = new PrivateKey();

        var validTx = swarmA.Blockchain.StagedTransactions.Add(validKey);
        var invalidTx = swarmA.Blockchain.StagedTransactions.Add(invalidKey);

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);

        await BootstrapAsync(swarmA, swarmB.Peer);

        swarmA.BroadcastTxs([validTx, invalidTx]);
        await swarmB.TxReceived.WaitAsync(default);

        Assert.Equal(swarmB.Blockchain.Transactions[validTx.Id], validTx);
        Assert.Throws<KeyNotFoundException>(
            () => swarmB.Blockchain.Transactions[invalidTx.Id]);

        Assert.Contains(validTx.Id, swarmB.Blockchain.StagedTransactions.Keys);
        Assert.DoesNotContain(invalidTx.Id, swarmB.Blockchain.StagedTransactions.Keys);
    }

    [RetryFact(Timeout = Timeout)]
    public async Task IgnoreTransactionFromDifferentGenesis()
    {
        var validKey = new PrivateKey();

        void IsSignerValid(Transaction tx)
        {
            var validAddress = validKey.Address;
            if (!tx.Signer.Equals(validAddress) && !tx.Signer.Equals(GenesisProposer.Address))
            {
                throw new InvalidOperationException("invalid signer");
            }
        }

        var policy = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validator = new RelayValidator<Transaction>(IsSignerValid),
            },
        };
        var fx1 = new MemoryRepositoryFixture();
        var fx2 = new MemoryRepositoryFixture();

        var swarmA = await CreateSwarm(
            MakeBlockChain(
                policy,
                privateKey: validKey,
                timestamp: DateTimeOffset.MinValue)).ConfigureAwait(false);
        var swarmB = await CreateSwarm(
            MakeBlockChain(
                policy,
                privateKey: validKey,
                timestamp: DateTimeOffset.MinValue.AddSeconds(1))).ConfigureAwait(false);

        try
        {
            var tx = swarmA.Blockchain.StagedTransactions.Add(validKey);

            await swarmA.StartAsync(default);
            await swarmB.StartAsync(default);

            await BootstrapAsync(swarmA, swarmB.Peer);

            swarmA.BroadcastTxs([tx]);
            await swarmB.TxReceived.WaitAsync(default);

            Assert.Throws<KeyNotFoundException>(() => swarmB.Blockchain.Transactions[tx.Id]);
            Assert.DoesNotContain(tx.Id, swarmB.Blockchain.StagedTransactions.Keys);
        }
        finally
        {
            CleaningSwarm(swarmA);
            CleaningSwarm(swarmB);

            fx1.Dispose();
            fx2.Dispose();
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotReceiveBlockFromNodeHavingDifferentGenesisBlock()
    {
        var keyA = ByteUtility.ParseHex(
            "8568eb6f287afedece2c7b918471183db0451e1a61535bb0381cfdf95b85df20");
        var keyB = ByteUtility.ParseHex(
            "c34f7498befcc39a14f03b37833f6c7bb78310f1243616524eda70e078b8313c");
        var keyC = ByteUtility.ParseHex(
            "941bc2edfab840d79914d80fe3b30840628ac37a5d812d7f922b5d2405a223d3");

        var privateKeyA = new PrivateKey(keyA);
        var privateKeyB = new PrivateKey(keyB);
        var privateKeyC = new PrivateKey(keyC);

        var signerAddress = new PrivateKey().Address;

        var actionsA = new[] { DumbAction.Create((signerAddress, "1")) };
        var actionsB = new[] { DumbAction.Create((signerAddress, "2")) };

        var genesisChainA = MakeBlockChain(
            new BlockchainOptions(),
            actionsA,
            null,
            privateKeyA);
        var genesisBlockA = genesisChainA.Genesis;
        var genesisChainB = MakeBlockChain(
            new BlockchainOptions(),
            actionsB,
            null,
            privateKeyB);
        var genesisChainC = MakeBlockChain(
            new BlockchainOptions(),
            genesisBlock: genesisBlockA);

        var swarmA =
            await CreateSwarm(genesisChainA, privateKeyA);
        var swarmB =
            await CreateSwarm(genesisChainB, privateKeyB);
        var swarmC =
            await CreateSwarm(genesisChainC, privateKeyC);
        try
        {
            await swarmA.StartAsync(default);
            await swarmB.StartAsync(default);
            await swarmC.StartAsync(default);

            await swarmB.AddPeersAsync([swarmA.Peer], default);
            await swarmC.AddPeersAsync([swarmA.Peer], default);

            var block = swarmA.Blockchain.ProposeBlock(privateKeyA);
            swarmA.Blockchain.Append(block, TestUtils.CreateBlockCommit(block));

            Task.WaitAll(
            [
                swarmC.BlockAppended.WaitAsync(default),
                Task.Run(() => swarmA.BroadcastBlock(block)),
            ]);

            Assert.NotEqual(genesisChainA.Genesis, genesisChainB.Genesis);
            Assert.Equal(genesisChainA.Blocks.Keys, genesisChainC.Blocks.Keys);
            Assert.Equal(2, genesisChainA.Blocks.Count);
            Assert.Equal(1, genesisChainB.Blocks.Count);
            Assert.Equal(2, genesisChainC.Blocks.Count);

            Assert.Equal(
                "1",
                genesisChainA
                    .GetWorld()
                    .GetAccount(SystemAddresses.SystemAccount)
                    .GetValue(signerAddress));
            Assert.Equal(
                "2",
                genesisChainB
                    .GetWorld()
                    .GetAccount(SystemAddresses.SystemAccount)
                    .GetValue(signerAddress));
            Assert.Equal(
                "1",
                genesisChainC
                    .GetWorld()
                    .GetAccount(SystemAddresses.SystemAccount)
                    .GetValue(signerAddress));
        }
        finally
        {
            CleaningSwarm(swarmA);
            CleaningSwarm(swarmB);
            CleaningSwarm(swarmC);
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task FindSpecificPeerAsync()
    {
        Swarm swarmA = await CreateSwarm();
        Swarm swarmB = await CreateSwarm();
        Swarm swarmC = await CreateSwarm();
        Swarm swarmD = await CreateSwarm();
        try
        {
            await swarmA.StartAsync(default);
            await swarmB.StartAsync(default);
            await swarmC.StartAsync(default);
            await swarmD.StartAsync(default);

            await swarmA.AddPeersAsync([swarmB.Peer], default);
            await swarmB.AddPeersAsync([swarmC.Peer], default);
            await swarmC.AddPeersAsync([swarmD.Peer], default);

            Peer foundPeer = await swarmA.FindPeerAsync(swarmB.Peer.Address, int.MaxValue, default);

            Assert.Equal(swarmB.Peer.Address, foundPeer.Address);
            Assert.DoesNotContain(swarmC.Peer, swarmA.Peers);

            foundPeer = await swarmA.FindPeerAsync(swarmD.Peer.Address, int.MaxValue, default);

            Assert.Equal(swarmD.Peer.Address, foundPeer.Address);
            Assert.Contains(swarmC.Peer, swarmA.Peers);
            Assert.Contains(swarmD.Peer, swarmA.Peers);
        }
        finally
        {
            CleaningSwarm(swarmA);
            CleaningSwarm(swarmB);
            CleaningSwarm(swarmC);
            CleaningSwarm(swarmD);
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task FindSpecificPeerAsyncFail()
    {
        Swarm swarmA = await CreateSwarm();
        Swarm swarmB = await CreateSwarm();
        Swarm swarmC = await CreateSwarm();
        try
        {
            await swarmA.StartAsync(default);
            await swarmB.StartAsync(default);
            await swarmC.StartAsync(default);

            await swarmA.AddPeersAsync([swarmB.Peer], default);
            await swarmB.AddPeersAsync([swarmC.Peer], default);

            CleaningSwarm(swarmB);

            Peer foundPeer = await swarmA.FindPeerAsync(swarmB.Peer.Address, int.MaxValue, default);

            Assert.Null(foundPeer);

            foundPeer = await swarmA.FindPeerAsync(swarmC.Peer.Address, int.MaxValue, default);

            Assert.Null(foundPeer);
            Assert.DoesNotContain(swarmC.Peer, swarmA.Peers);
        }
        finally
        {
            CleaningSwarm(swarmA);
            CleaningSwarm(swarmC);
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task FindSpecificPeerAsyncDepthFail()
    {
        Swarm swarmA = await CreateSwarm();
        Swarm swarmB = await CreateSwarm();
        Swarm swarmC = await CreateSwarm();
        Swarm swarmD = await CreateSwarm();

        // _output.WriteLine("{0}: {1}", nameof(swarmA), swarmA.Peer);
        // _output.WriteLine("{0}: {1}", nameof(swarmB), swarmB.Peer);
        // _output.WriteLine("{0}: {1}", nameof(swarmC), swarmC.Peer);
        // _output.WriteLine("{0}: {1}", nameof(swarmD), swarmD.Peer);

        try
        {
            await swarmA.StartAsync(default);
            await swarmB.StartAsync(default);
            await swarmC.StartAsync(default);
            await swarmD.StartAsync(default);

            await swarmA.AddPeersAsync([swarmB.Peer], default);
            await swarmB.AddPeersAsync([swarmC.Peer], default);
            await swarmC.AddPeersAsync([swarmD.Peer], default);

            Peer foundPeer = await swarmA.FindPeerAsync(swarmC.Peer.Address, 1, default);

            Assert.Equal(swarmC.Peer.Address, foundPeer.Address);
            swarmA.RoutingTable.Clear();
            Assert.Empty(swarmA.Peers);
            await swarmA.AddPeersAsync([swarmB.Peer], default);

            foundPeer = await swarmA.FindPeerAsync(swarmD.Peer.Address, 1, default);

            Assert.Null(foundPeer);
        }
        finally
        {
            CleaningSwarm(swarmA);
            CleaningSwarm(swarmB);
            CleaningSwarm(swarmC);
            CleaningSwarm(swarmD);
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task DoNotFillWhenGetAllBlockAtFirstTimeFromSender()
    {
        Swarm receiver = await CreateSwarm();
        Swarm sender = await CreateSwarm();
        await receiver.StartAsync(default);
        await sender.StartAsync(default);

        receiver.FindNextHashesChunkSize = 8;
        sender.FindNextHashesChunkSize = 8;
        Blockchain chain = sender.Blockchain;

        for (int i = 0; i < 6; i++)
        {
            Block block = chain.ProposeBlock(GenesisProposer);
            chain.Append(block, TestUtils.CreateBlockCommit(block));
        }

        Log.Debug("Sender's BlockChain Tip index: #{index}", sender.Blockchain.Tip.Height);

        try
        {
            await BootstrapAsync(sender, receiver.Peer);

            sender.BroadcastBlock(sender.Blockchain.Tip);

            await receiver.BlockReceived.WaitAsync(default);
            await receiver.BlockAppended.WaitAsync(default);
            Assert.Equal(
                7,
                receiver.Blockchain.Blocks.Count);
        }
        finally
        {
            CleaningSwarm(receiver);
            CleaningSwarm(sender);
        }
    }

    [Fact(Timeout = Timeout)]
    public async Task FillWhenGetAChunkOfBlocksFromSender()
    {
        Swarm receiver = await CreateSwarm();
        Swarm sender = await CreateSwarm();
        await receiver.StartAsync(default);
        await sender.StartAsync(default);

        receiver.FindNextHashesChunkSize = 2;
        sender.FindNextHashesChunkSize = 2;
        Blockchain chain = sender.Blockchain;

        for (int i = 0; i < 6; i++)
        {
            Block block = chain.ProposeBlock(GenesisProposer);
            chain.Append(block, TestUtils.CreateBlockCommit(block));
        }

        Log.Debug("Sender's BlockChain Tip index: #{index}", sender.Blockchain.Tip.Height);

        try
        {
            await BootstrapAsync(sender, receiver.Peer);

            sender.BroadcastBlock(sender.Blockchain.Tip);

            await receiver.BlockReceived.WaitAsync(default);
            await receiver.BlockAppended.WaitAsync(default);
            Log.Debug("Count: {Count}", receiver.Blockchain.Blocks.Count);
            Assert.Equal(
                2,
                receiver.Blockchain.Blocks.Count);
        }
        finally
        {
            CleaningSwarm(receiver);
            CleaningSwarm(sender);
        }
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

        try
        {
            await BootstrapAsync(sender, receiver.Peer);

            sender.BroadcastBlock(sender.Blockchain.Tip);

            await receiver.BlockReceived.WaitAsync(default);
            await receiver.BlockAppended.WaitAsync(default);
            Log.Debug("Count: {Count}", receiver.Blockchain.Blocks.Count);
            sender.BroadcastBlock(sender.Blockchain.Tip);
            Assert.Equal(
                3,
                receiver.Blockchain.Blocks.Count);

            sender.BroadcastBlock(sender.Blockchain.Tip);

            await receiver.BlockReceived.WaitAsync(default);
            await receiver.BlockAppended.WaitAsync(default);
            Log.Debug("Count: {Count}", receiver.Blockchain.Blocks.Count);
            sender.BroadcastBlock(sender.Blockchain.Tip);
            Assert.Equal(
                5,
                receiver.Blockchain.Blocks.Count);

            sender.BroadcastBlock(sender.Blockchain.Tip);

            await receiver.BlockReceived.WaitAsync(default);
            await receiver.BlockAppended.WaitAsync(default);
            Log.Debug("Count: {Count}", receiver.Blockchain.Blocks.Count);
            sender.BroadcastBlock(sender.Blockchain.Tip);
            Assert.Equal(
                7,
                receiver.Blockchain.Blocks.Count);
        }
        finally
        {
            CleaningSwarm(receiver);
            CleaningSwarm(sender);
        }
    }

    [RetryFact(10, Timeout = Timeout)]
    public async Task GetPeerChainStateAsync()
    {
        var key2 = new PrivateKey();

        Swarm swarm1 = await CreateSwarm().ConfigureAwait(false);
        Swarm swarm2 = await CreateSwarm(key2).ConfigureAwait(false);
        Swarm swarm3 = await CreateSwarm().ConfigureAwait(false);

        var peerChainState = await swarm1.GetPeerChainStateAsync(
            TimeSpan.FromSeconds(1), default);
        Assert.Empty(peerChainState);

        try
        {
            await swarm2.StartAsync(default);
            await swarm3.StartAsync(default);

            await BootstrapAsync(swarm1, swarm2.Peer);

            peerChainState = await swarm1.GetPeerChainStateAsync(
                TimeSpan.FromSeconds(1), default);
            Assert.Equal(
                new PeerChainState(swarm2.Peer, 0),
                peerChainState.First());

            Block block = swarm2.Blockchain.ProposeBlock(key2);
            swarm2.Blockchain.Append(block, TestUtils.CreateBlockCommit(block));
            peerChainState = await swarm1.GetPeerChainStateAsync(
                TimeSpan.FromSeconds(1), default);
            Assert.Equal(
                new PeerChainState(swarm2.Peer, 1),
                peerChainState.First());

            await BootstrapAsync(swarm1, swarm3.Peer);
            peerChainState = await swarm1.GetPeerChainStateAsync(
                TimeSpan.FromSeconds(1), default);
            Assert.Equal(
                new[]
                {
                    new PeerChainState(swarm2.Peer, 1),
                    new PeerChainState(swarm3.Peer, 0),
                }.ToHashSet(),
                peerChainState.ToHashSet());
        }
        finally
        {
            CleaningSwarm(swarm2);
            CleaningSwarm(swarm3);
        }
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

        var key = new PrivateKey();
        Swarm swarm = await CreateSwarm(options: options)
            .ConfigureAwait(false);
        var transport = swarm.Transport;

        try
        {
            await swarm.StartAsync(default);
            await transport.StartAsync(default);
            var tasks = new List<Task>();
            var content = new GetBlockMessage { BlockHashes = [swarm.Blockchain.Genesis.BlockHash] };
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(
                    Task.Run(async () => transport.Post(swarm.Peer, content)));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
                // ignored
            }

            Assert.Equal(
                options.TaskRegulationOptions.MaxTransferBlocksTaskCount,
                tasks.Count(t => t.IsCompletedSuccessfully));
        }
        finally
        {
            CleaningSwarm(swarm);
            await transport.StopAsync(default);
        }
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

        var key = new PrivateKey();
        Swarm swarm = await CreateSwarm(
                options: options)
            .ConfigureAwait(false);
        var transport = swarm.Transport;

        try
        {
            await swarm.StartAsync(default);
            var fx = new MemoryRepositoryFixture();
            await transport.StartAsync(default);
            var tasks = new List<Task>();
            var content = new GetTransactionMessage { TxIds = [fx.TxId1] };
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(
                    Task.Run(async () => transport.Post(swarm.Peer, content)));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
                // ignored
            }

            Assert.Equal(
                options.TaskRegulationOptions.MaxTransferBlocksTaskCount,
                tasks.Count(t => t.IsCompletedSuccessfully));
        }
        finally
        {
            CleaningSwarm(swarm);
            await transport.StopAsync(default);
        }
    }

    [Obsolete("unused")]
    private async Task StopAsync(Swarm swarm)
    {
        using var cts = new CancellationTokenSource(10000);
        await swarm.StopAsync(cts.Token);
    }

    [Obsolete("unused")]
    private async Task CleaningSwarm(Swarm swarm)
    {
        using var cts = new CancellationTokenSource(10000);
        await swarm.StopAsync(cts.Token);
        await swarm.DisposeAsync();
    }

    private Task BootstrapAsync(
        Swarm swarm,
        Peer seed,
        CancellationToken cancellationToken = default) =>
        BootstrapAsync(swarm, [seed], cancellationToken);

    private async Task BootstrapAsync(
        Swarm swarm,
        IEnumerable<Peer> seeds,
        CancellationToken cancellationToken = default)
    {
        // await swarm.BootstrapAsync(
        //     seeds,
        //     dialTimeout: TimeSpan.FromSeconds(3),
        //     searchDepth: Kademlia.MaxDepth,
        //     cancellationToken: cancellationToken);
    }

    private async Task TurnProxy(
        int port,
        Uri turnUri,
        CancellationToken cancellationToken)
    {
        var server = new TcpListener(IPAddress.Loopback, port);
        server.Start();
        var tasks = new List<Task>();
        var clients = new List<TcpClient>();

        cancellationToken.Register(() => server.Stop());
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await server.AcceptTcpClientAsync();
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            clients.Add(client);

            tasks.Add(Task.Run(
                async () =>
                {
                    const int bufferSize = 8192;
                    NetworkStream stream = client.GetStream();

                    using (TcpClient remoteClient = new TcpClient(turnUri.Host, turnUri.Port))
                    {
                        var remoteStream = remoteClient.GetStream();
                        await await Task.WhenAny(
                            remoteStream.CopyToAsync(stream, bufferSize, cancellationToken),
                            stream.CopyToAsync(remoteStream, bufferSize, cancellationToken));
                    }

                    client.Dispose();
                },
                cancellationToken));
        }

        foreach (var client in clients)
        {
            client?.Dispose();
        }

        Log.Debug("TurnProxy is canceled");

        await Task.WhenAny(tasks);
    }
}
