using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.NetMQ;
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
using Libplanet.Types.Threading;
using Libplanet.Net.Services;
using Libplanet.Tests;
using Libplanet.Net.MessageHandlers;
using Libplanet.Net.Components;

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
        swarmC.PeerDiscovery.AddOrUpdate(swarmD.Peer);
        await swarmB.AddPeersAsync([swarmA.Peer], default);
        await swarmC.AddPeersAsync([swarmA.Peer], default);

        Assert.Contains(swarmB.Peer, swarmC.Peers);
        Assert.Contains(swarmC.Peer, swarmB.Peers);
        foreach (var peer in swarmB.PeerDiscovery.Peers)
        {
            var peerState = swarmB.PeerDiscovery.Peers.GetState(peer.Address);
            Assert.InRange(peerState.LastUpdated, bootstrappedAt, DateTimeOffset.UtcNow);
        }

        foreach (var peer in swarmC.PeerDiscovery.Peers)
        {
            var peerState = swarmC.PeerDiscovery.Peers.GetState(peer.Address);
            if (peer.Address == swarmD.Peer.Address)
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
        await swarm.PeerDiscovery.RefreshAsync(
            TimeSpan.Zero,
            default);
        // Invoke once more in case of swarmA and swarmB is in the same bucket,
        // and swarmA is last updated.
        await swarm.PeerDiscovery.RefreshAsync(
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
            new ConsensusServiceOptions
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

        transportA.MessageHandlers.Add(new BlockHashRequestMessageHandler(blockchainA, transportA));
        transportA.MessageHandlers.Add(new BlockRequestMessageHandler(blockchainA, transportA, 1));

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

        await using var swarmA = await CreateSwarm(keyA);
        var genesis = swarmA.Blockchain.Genesis;
        await using var swarmB = await CreateSwarm(keyB, genesis: genesis);

        var blockchainA = swarmA.Blockchain;
        var block1 = blockchainA.ProposeBlock(keyA);
        blockchainA.Append(block1, TestUtils.CreateBlockCommit(block1));
        var block2 = blockchainA.ProposeBlock(keyA);
        blockchainA.Append(block2, TestUtils.CreateBlockCommit(block2));

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);

        var peer = swarmA.Peer;

        await swarmB.AddPeersAsync([peer], default);

        var hashes = await swarmB.Transport.GetBlockHashesAsync(
            peer,
            genesis.BlockHash,
            default);

        var transportB = swarmB.Transport;
        var request = new BlockRequestMessage { BlockHashes = [.. hashes], ChunkSize = 2 };
        var responses = await transportB.SendAsync<BlockResponseMessage>(
            peer, request, response => response.IsLast, default).ToArrayAsync();

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
        using var fx = new MemoryRepositoryFixture();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport(keyB);
        var blockchainA = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainB = MakeBlockchain(genesisBlock: fx.GenesisBlock);
        var fetcherA = new TransactionFetcher(blockchainA, transportA);
        var txKey = new PrivateKey();
        var tx = new TransactionBuilder
        {
            GenesisHash = blockchainB.Genesis.BlockHash,
        }.Create(txKey);
        blockchainB.StagedTransactions.Add(tx);
        blockchainB.ProposeAndAppend(keyB);

        transportB.MessageHandlers.Add(new TransactionRequestMessageHandler(blockchainB, transportB, 1));

        await transportA.StartAsync(default);
        await transportB.StartAsync(default);

        var txs = await fetcherA.FetchAsync(transportB.Peer, [tx.Id], default);
        Assert.Equal(new[] { tx }, txs);
    }

    [Fact(Timeout = Timeout)]
    public async Task CanResolveEndPoint()
    {
        var random = RandomUtility.GetRandom(output);
        var peer = RandomUtility.LocalPeer(random);
        var transportOptions = new TransportOptions
        {
            Host = peer.EndPoint.Host,
            Port = peer.EndPoint.Port,
        };
        var options = new SwarmOptions
        {
            TransportOptions = transportOptions,
        };
        await using var swarm = await CreateSwarm(options: options);
        Assert.Equal(peer.EndPoint, swarm.Peer.EndPoint);
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

    [Fact(Timeout = Timeout)]
    public async Task CannotBlockSyncWithForkedChain()
    {
        var blockchainOptions = new BlockchainOptions();
        var blockchain1 = MakeBlockchain(blockchainOptions);
        var blockchain2 = MakeBlockchain(blockchainOptions);

        var key1 = new PrivateKey();
        var key2 = new PrivateKey();

        await using var swarm1 = await CreateSwarm(blockchain1, key1);
        await using var swarm2 = await CreateSwarm(blockchain2, key2);

        var privateKey = new PrivateKey();
        var addr = swarm1.Address;
        var item = "foo";

        blockchain1.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = [DumbAction.Create((addr, item))],
        });
        var block1 = blockchain1.ProposeBlock(key1);
        blockchain1.Append(block1, TestUtils.CreateBlockCommit(block1));
        var tipHash1 = blockchain1.Tip.BlockHash;

        blockchain2.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = [DumbAction.Create((addr, item))],
        });
        var block2 = blockchain2.ProposeBlock(key2);
        blockchain2.Append(block2, TestUtils.CreateBlockCommit(block2));

        blockchain2.StagedTransactions.Add(privateKey, submission: new()
        {
            Actions = [DumbAction.Create((addr, item))],
        });
        var latest = blockchain2.ProposeBlock(key2);
        blockchain2.Append(latest, TestUtils.CreateBlockCommit(latest));

        await swarm1.StartAsync(default);
        await swarm2.StartAsync(default);

        await swarm2.AddPeersAsync([swarm1.Peer], default);

        swarm2.BroadcastBlock(latest);

        await Task.Delay(5_000);
        Assert.Equal(tipHash1, swarm1.Blockchain.Tip.BlockHash);
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
            MakeBlockchain(blockchainOptions, privateKey: validKey));
        await using var swarmB = await CreateSwarm(
            MakeBlockchain(blockchainOptions, privateKey: validKey));

        var invalidKey = new PrivateKey();

        var validTx = swarmA.Blockchain.StagedTransactions.Add(validKey);
        var invalidTx = swarmA.Blockchain.StagedTransactions.Add(invalidKey);

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);

        await swarmA.AddPeersAsync([swarmB.Peer], default);

        swarmA.BroadcastTxs([validTx, invalidTx]);
        // await swarmB.TxReceived.WaitAsync(default);

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

        var blockchainOptions = new BlockchainOptions
        {
            TransactionOptions = new TransactionOptions
            {
                Validator = new RelayValidator<Transaction>(IsSignerValid),
            },
        };
        using var fx1 = new MemoryRepositoryFixture();
        using var fx2 = new MemoryRepositoryFixture();

        var swarmA = await CreateSwarm(
            MakeBlockchain(blockchainOptions, privateKey: validKey, timestamp: DateTimeOffset.MinValue));
        var swarmB = await CreateSwarm(
            MakeBlockchain(blockchainOptions, privateKey: validKey, timestamp: DateTimeOffset.MinValue.AddSeconds(1)));

        var tx = swarmA.Blockchain.StagedTransactions.Add(validKey);

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);

        await swarmA.AddPeersAsync([swarmB.Peer], default);

        swarmA.BroadcastTxs([tx]);
        // await swarmB.TxReceived.WaitAsync(default);

        Assert.Throws<KeyNotFoundException>(() => swarmB.Blockchain.Transactions[tx.Id]);
        Assert.DoesNotContain(tx.Id, swarmB.Blockchain.StagedTransactions.Keys);
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

        var genesisChainA = MakeBlockchain(
            new BlockchainOptions(),
            actionsA,
            null,
            privateKeyA);
        var genesisBlockA = genesisChainA.Genesis;
        var genesisChainB = MakeBlockchain(
            new BlockchainOptions(),
            actionsB,
            null,
            privateKeyB);
        var genesisChainC = MakeBlockchain(
            new BlockchainOptions(),
            genesisBlock: genesisBlockA);

        var swarmA =
            await CreateSwarm(genesisChainA, privateKeyA);
        var swarmB =
            await CreateSwarm(genesisChainB, privateKeyB);
        var swarmC =
            await CreateSwarm(genesisChainC, privateKeyC);

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);
        await swarmC.StartAsync(default);

        await swarmB.AddPeersAsync([swarmA.Peer], default);
        await swarmC.AddPeersAsync([swarmA.Peer], default);

        var block = swarmA.Blockchain.ProposeBlock(privateKeyA);
        swarmA.Blockchain.Append(block, TestUtils.CreateBlockCommit(block));

        Task.WaitAll(
        [
            // swarmC.BlockAppended.WaitAsync(default),
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

    [Fact(Timeout = Timeout)]
    public async Task FindSpecificPeerAsync()
    {
        await using var swarmA = await CreateSwarm();
        await using var swarmB = await CreateSwarm();
        await using var swarmC = await CreateSwarm();
        await using var swarmD = await CreateSwarm();
        var peerServiceA = swarmA.PeerDiscovery;

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);
        await swarmC.StartAsync(default);
        await swarmD.StartAsync(default);

        await swarmA.AddPeersAsync([swarmB.Peer], default);
        await swarmB.AddPeersAsync([swarmC.Peer], default);
        await swarmC.AddPeersAsync([swarmD.Peer], default);


        var foundPeer1 = await peerServiceA.FindPeerAsync(swarmB.Peer.Address, int.MaxValue, default);

        Assert.Equal(swarmB.Peer.Address, foundPeer1.Address);
        Assert.DoesNotContain(swarmC.Peer, swarmA.Peers);

        var foundPeer2 = await peerServiceA.FindPeerAsync(swarmD.Peer.Address, int.MaxValue, default);

        Assert.Equal(swarmD.Peer.Address, foundPeer2.Address);
        Assert.Contains(swarmC.Peer, swarmA.Peers);
        Assert.Contains(swarmD.Peer, swarmA.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task FindSpecificPeerAsyncFail()
    {
        await using var swarmA = await CreateSwarm();
        await using var swarmB = await CreateSwarm();
        await using var swarmC = await CreateSwarm();
        var peerServiceA = swarmA.PeerDiscovery;

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);
        await swarmC.StartAsync(default);

        await swarmA.AddPeersAsync([swarmB.Peer], default);
        await swarmB.AddPeersAsync([swarmC.Peer], default);

        await swarmB.DisposeAsync();

        Peer foundPeer = await peerServiceA.FindPeerAsync(swarmB.Peer.Address, int.MaxValue, default);

        Assert.Null(foundPeer);

        foundPeer = await peerServiceA.FindPeerAsync(swarmC.Peer.Address, int.MaxValue, default);

        Assert.Null(foundPeer);
        Assert.DoesNotContain(swarmC.Peer, swarmA.Peers);
    }

    [Fact(Timeout = Timeout)]
    public async Task FindSpecificPeerAsyncDepthFail()
    {
        await using var swarmA = await CreateSwarm();
        await using var swarmB = await CreateSwarm();
        await using var swarmC = await CreateSwarm();
        await using var swarmD = await CreateSwarm();
        var peerServiceA = swarmA.PeerDiscovery;

        await swarmA.StartAsync(default);
        await swarmB.StartAsync(default);
        await swarmC.StartAsync(default);
        await swarmD.StartAsync(default);

        await swarmA.AddPeersAsync([swarmB.Peer], default);
        await swarmB.AddPeersAsync([swarmC.Peer], default);
        await swarmC.AddPeersAsync([swarmD.Peer], default);

        var foundPeer1 = await peerServiceA.FindPeerAsync(swarmC.Peer.Address, 1, default);

        Assert.Equal(swarmC.Peer.Address, foundPeer1.Address);
        peerServiceA.Peers.Clear();
        Assert.Empty(swarmA.Peers);
        await swarmA.AddPeersAsync([swarmB.Peer], default);

        var foundPeer2 = await peerServiceA.FindPeerAsync(swarmD.Peer.Address, 1, default);

        Assert.Null(foundPeer2);
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

        await sender.AddPeersAsync([receiver.Peer], default);

        sender.BroadcastBlock(sender.Blockchain.Tip);

        // await receiver.BlockReceived.WaitAsync(default);
        // await receiver.BlockAppended.WaitAsync(default);
        Assert.Equal(
            7,
            receiver.Blockchain.Blocks.Count);
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

        await sender.AddPeersAsync([receiver.Peer], default);

        sender.BroadcastBlock(sender.Blockchain.Tip);

        // await receiver.BlockReceived.WaitAsync(default);
        // await receiver.BlockAppended.WaitAsync(default);
        Log.Debug("Count: {Count}", receiver.Blockchain.Blocks.Count);
        Assert.Equal(
            2,
            receiver.Blockchain.Blocks.Count);
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

        await using var swarm1 = await CreateSwarm();
        await using var swarm2 = await CreateSwarm(key2);
        await using var swarm3 = await CreateSwarm();
        var blockchain2 = swarm2.Blockchain;
        var blockchain3 = swarm3.Blockchain;

        var blockchainStates1 = await swarm1.GetBlockchainStateAsync(TimeSpan.FromSeconds(1), default)
            .ToArrayAsync(default);
        Assert.Empty(blockchainStates1);

        await swarm1.StartAsync(default);
        await swarm2.StartAsync(default);
        await swarm3.StartAsync(default);

        await swarm1.AddPeersAsync([swarm2.Peer], default);

        var blockchainStates2 = await swarm1.GetBlockchainStateAsync(TimeSpan.FromSeconds(1), default)
            .ToArrayAsync(default);
        Assert.Equal(
            new BlockchainState(swarm2.Peer, blockchain2.Genesis, blockchain2.Genesis),
            blockchainStates2[0]);

        var block = blockchain2.ProposeBlock(key2);
        blockchain2.Append(block, TestUtils.CreateBlockCommit(block));

        var blockchainStates3 = await swarm1.GetBlockchainStateAsync(TimeSpan.FromSeconds(1), default)
            .ToArrayAsync(default);
        Assert.Equal(
            new BlockchainState(swarm2.Peer, blockchain2.Genesis, blockchain2.Tip),
            blockchainStates3[0]);

        await swarm1.AddPeersAsync([swarm3.Peer], default);
        var blockchainStates4 = await swarm1.GetBlockchainStateAsync(TimeSpan.FromSeconds(1), default)
            .ToArrayAsync(default);
        Assert.Equal(
            [
                new BlockchainState(swarm2.Peer, blockchain2.Genesis, blockchain2.Tip),
                new BlockchainState(swarm3.Peer, blockchain3.Genesis, blockchain3.Tip),
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
