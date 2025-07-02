using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Protocols;
using Libplanet.Net.Transports;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Tests.Store;
using Libplanet.Types;
using NetMQ;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;
using Serilog;
using xRetry;
using Xunit.Abstractions;
using Libplanet.TestUtilities;
using Libplanet.Extensions;


#if NETFRAMEWORK && (NET47 || NET471)
using static Libplanet.Tests.HashSetExtensions;
#endif
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Net.Tests
{
    [Collection("NetMQConfiguration")]
    public partial class SwarmTest : IDisposable
    {
        private const int Timeout = 60 * 1000;

        private readonly ITestOutputHelper _output;
        private readonly ILogger _logger;

        private bool _disposed = false;

        public SwarmTest(ITestOutputHelper output)
        {
            const string outputTemplate =
                "{Timestamp:HH:mm:ss:ffffffZ}[@{SwarmId}][{ThreadId}] - {Message}";
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.WithThreadId()
                .WriteTo.TestOutput(output, outputTemplate: outputTemplate)
                .CreateLogger()
                .ForContext<SwarmTest>();

            _logger = Log.ForContext<SwarmTest>();
            _output = output;

            _finalizers = new List<Func<Task>>();
        }

        ~SwarmTest()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        [Fact(Timeout = Timeout)]
        public async Task CanNotStartTwice()
        {
            Swarm swarm = await CreateSwarm();

            Task t = await Task.WhenAny(
                swarm.StartAsync(default),
                swarm.StartAsync(default));

            Assert.True(swarm.IsRunning);
            Assert.True(t.IsFaulted);
            Assert.True(
                t.Exception.InnerException is SwarmException,
                $"Expected SwarmException, but actual exception was: {t.Exception.InnerException}");

            await CleaningSwarm(swarm);
        }

        [Fact(Timeout = Timeout)]
        public async Task HandleReconnection()
        {
            Swarm seed = await CreateSwarm();

            var privateKey = new PrivateKey();
            Swarm swarmA =
                await CreateSwarm(privateKey: privateKey);
            Swarm swarmB =
                await CreateSwarm(privateKey: privateKey);

            try
            {
                await StartAsync(seed);
                await StartAsync(swarmA);
                await StartAsync(swarmB);
                await swarmA.AddPeersAsync([seed.Peer], default);
                await StopAsync(swarmA);
                await swarmB.AddPeersAsync([seed.Peer], default);

                Assert.Contains(swarmB.Peer, seed.Peers);
                Assert.Contains(seed.Peer, swarmB.Peers);
            }
            finally
            {
                CleaningSwarm(seed);
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task RunConsensusReactorIfOptionGiven()
        {
            Swarm swarmA = await CreateSwarm();
            Swarm swarmB = await CreateConsensusSwarm();

            await StartAsync(swarmA);
            await StartAsync(swarmB);
            await Task.Delay(1000);

            Assert.True(swarmA.IsRunning);
            Assert.True(swarmB.IsRunning);
            Assert.False(swarmA.ConsensusRunning);
            Assert.True(swarmB.ConsensusRunning);

            CleaningSwarm(swarmA);
            CleaningSwarm(swarmB);
        }

        [Fact(Timeout = Timeout)]
        public async Task StopAsyncTest()
        {
            Swarm swarm = await CreateSwarm();

            await swarm.StopAsync(default);
            var task = await StartAsync(swarm);

            Assert.True(swarm.IsRunning);
            await swarm.StopAsync(default);

            Assert.False(swarm.IsRunning);

            Assert.False(
                task.IsFaulted,
                $"A task was faulted due to an exception: {task.Exception}");
        }

        [Fact(Timeout = Timeout)]
        public async Task CanWaitForRunning()
        {
            Swarm swarm = await CreateSwarm();

            Assert.False(swarm.IsRunning);

            Task consumerTask = Task.Run(
                async () =>
                {
                    Assert.True(swarm.IsRunning);
                });

            Task producerTask = Task.Run(async () => { await swarm.StartAsync(default); });

            await consumerTask;
            Assert.True(swarm.IsRunning);

            CleaningSwarm(swarm);
            Assert.False(swarm.IsRunning);
        }

        [Fact(Timeout = Timeout)]
        public async Task AddPeersWithoutStart()
        {
            Swarm a = await CreateSwarm();
            Swarm b = await CreateSwarm();

            try
            {
                await StartAsync(b);

                await a.AddPeersAsync([b.Peer], default);

                Assert.Contains(b.Peer, a.Peers);
                Assert.Contains(a.Peer, b.Peers);
            }
            finally
            {
                CleaningSwarm(a);
                CleaningSwarm(b);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task AddPeersAsync()
        {
            Swarm a = await CreateSwarm();
            Swarm b = await CreateSwarm();

            try
            {
                await StartAsync(a);
                await StartAsync(b);

                await a.AddPeersAsync([b.Peer], default);

                Assert.Contains(a.Peer, b.Peers);
                Assert.Contains(b.Peer, a.Peers);
            }
            finally
            {
                CleaningSwarm(a);
                CleaningSwarm(b);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task BootstrapException()
        {
            Swarm swarmA = await CreateSwarm();
            Swarm swarmB = await CreateSwarm();

            try
            {
                // await Assert.ThrowsAsync<InvalidOperationException>(
                //     () => swarmB.BootstrapAsync(
                //         [swarmA.AsPeer],
                //         TimeSpan.FromMilliseconds(3000),
                //         Kademlia.MaxDepth));

                await StartAsync(swarmA);
            }
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task BootstrapAsyncWithoutStart()
        {
            Swarm swarmA = await CreateSwarm();
            Swarm swarmB = await CreateSwarm();
            Swarm swarmC = await CreateSwarm();
            Swarm swarmD = await CreateSwarm();

            try
            {
                await StartAsync(swarmA);
                await StartAsync(swarmB);
                await StartAsync(swarmD);

                var bootstrappedAt = DateTimeOffset.UtcNow;
                swarmC.RoutingTable.AddPeer(swarmD.Peer);
                await BootstrapAsync(swarmB, swarmA.Peer);
                await BootstrapAsync(swarmC, swarmA.Peer);

                Assert.Contains(swarmB.Peer, swarmC.Peers);
                Assert.Contains(swarmC.Peer, swarmB.Peers);
                foreach (PeerState state in swarmB.RoutingTable.PeerStates)
                {
                    Assert.InRange(state.LastUpdated, bootstrappedAt, DateTimeOffset.UtcNow);
                }

                foreach (PeerState state in swarmC.RoutingTable.PeerStates)
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
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
                CleaningSwarm(swarmC);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task MaintainStaticPeers()
        {
            var keyA = new PrivateKey();
            var hostOptionsA = new TransportOptions { Host = IPAddress.Loopback.ToString(), Port = 20_000 };
            var hostOptionsB = new TransportOptions { Host = IPAddress.Loopback.ToString(), Port = 20_001 };

            Swarm swarmA =
                await CreateSwarm(keyA, transportOptions: hostOptionsA);
            Swarm swarmB =
                await CreateSwarm(transportOptions: hostOptionsB);
            await StartAsync(swarmA);
            await StartAsync(swarmB);

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

            await StartAsync(swarm);
            await AssertThatEventually(() => swarm.Peers.Contains(swarmA.Peer), 5_000);
            await AssertThatEventually(() => swarm.Peers.Contains(swarmB.Peer), 5_000);

            _logger.Debug("Address of swarmA: {Address}", swarmA.Address);
            await CleaningSwarm(swarmA);
            await swarmA.DisposeAsync();
            await Task.Delay(100);
            await swarm.PeerDiscovery.RefreshTableAsync(
                TimeSpan.Zero,
                default);
            // Invoke once more in case of swarmA and swarmB is in the same bucket,
            // and swarmA is last updated.
            await swarm.PeerDiscovery.RefreshTableAsync(
                TimeSpan.Zero,
                default);
            Assert.DoesNotContain(swarmA.Peer, swarm.Peers);
            Assert.Contains(swarmB.Peer, swarm.Peers);

            Swarm swarmC =
                await CreateSwarm(keyA, transportOptions: hostOptionsA);
            await StartAsync(swarmC);
            await AssertThatEventually(() => swarm.Peers.Contains(swarmB.Peer), 5_000);
            await AssertThatEventually(() => swarm.Peers.Contains(swarmC.Peer), 5_000);

            CleaningSwarm(swarm);
            CleaningSwarm(swarmB);
            CleaningSwarm(swarmC);
        }

        [Fact(Timeout = Timeout)]
        public async Task Cancel()
        {
            Swarm swarm = await CreateSwarm();
            var cts = new CancellationTokenSource();

            Task task = await StartAsync(
                swarm,
                cancellationToken: cts.Token);

            await Task.Delay(100);
            cts.Cancel();
            await Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
            CleaningSwarm(swarm);
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
                }).ToImmutableArray();
            var reactorOpts = Enumerable.Range(0, 4).Select(i =>
                new ConsensusReactorOptions
                {
                    Validators = consensusPeers,
                    Port = 6000 + i,
                    Workers = 100,
                    TargetBlockInterval = TimeSpan.FromSeconds(10),
                    ConsensusOptions = new ConsensusOptions(),
                }).ToList();
            var swarms = new List<Swarm>();
            for (int i = 0; i < 4; i++)
            {
                swarms.Add(await CreateSwarm(
                    privateKey: TestUtils.PrivateKeys[i],
                    transportOptions: new TransportOptions
                    {
                        Host = "127.0.0.1",
                        Port = 9000 + i,
                    },
                    policy: policy,
                    genesis: genesis,
                    consensusReactorOption: reactorOpts[i]));
            }

            try
            {
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
            finally
            {
                CleaningSwarm(swarms[0]);
                CleaningSwarm(swarms[1]);
                CleaningSwarm(swarms[2]);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task GetBlocks()
        {
            var keyA = new PrivateKey();

            Swarm swarmA =
                await CreateSwarm(keyA);
            Block genesis = swarmA.Blockchain.Genesis;
            Swarm swarmB =
                await CreateSwarm(genesis: genesis);

            Blockchain chainA = swarmA.Blockchain;

            Block block1 = chainA.ProposeBlock(keyA);
            chainA.Append(block1, TestUtils.CreateBlockCommit(block1));
            Block block2 = chainA.ProposeBlock(keyA);
            chainA.Append(block2, TestUtils.CreateBlockCommit(block2));

            try
            {
                await StartAsync(swarmA);
                await StartAsync(swarmB);

                await swarmA.AddPeersAsync([swarmB.Peer], default);

                var inventories = await swarmB.GetBlockHashes(
                    swarmA.Peer,
                    genesis.BlockHash);
                Assert.Equal(
                    new[] { genesis.BlockHash, block1.BlockHash, block2.BlockHash },
                    inventories);

                (Block, BlockCommit)[] receivedBlocks =
                    await swarmB.GetBlocksAsync(
                        swarmA.Peer,
                        inventories,
                        cancellationToken: default)
                    .ToArrayAsync();
                Assert.Equal(
                    [genesis, block1, block2], receivedBlocks.Select(pair => pair.Item1));
            }
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task GetMultipleBlocksAtOnce()
        {
            var keyA = new PrivateKey();
            var keyB = new PrivateKey();

            Swarm swarmA = await CreateSwarm(keyA);
            Block genesis = swarmA.Blockchain.Genesis;
            Swarm swarmB =
                await CreateSwarm(keyB, genesis: genesis);

            Blockchain chainA = swarmA.Blockchain;
            Blockchain chainB = swarmB.Blockchain;

            Block block1 = chainA.ProposeBlock(keyA);
            chainA.Append(block1, TestUtils.CreateBlockCommit(block1));
            Block block2 = chainA.ProposeBlock(keyA);
            chainA.Append(block2, TestUtils.CreateBlockCommit(block2));

            try
            {
                await StartAsync(swarmA);
                await StartAsync(swarmB);

                var peer = swarmA.Peer;

                await swarmB.AddPeersAsync([peer], default);

                var hashes = await swarmB.GetBlockHashes(
                    peer,
                    genesis.BlockHash);

                ITransport transport = swarmB.Transport;

                var request = new GetBlocksMessage { BlockHashes = [.. hashes], ChunkSize = 2 };
                var reply = await transport.SendMessageAsync(
                    swarmA.Peer, request, default);
                var aggregateMessage = (AggregateMessage)reply.Message;
                var responses = aggregateMessage.Messages;

                var blockMessage = (BlocksMessage)responses[0];

                Assert.Equal(2, responses.Length);
                Assert.Equal(4, blockMessage.Payloads.Length);

                blockMessage = (BlocksMessage)responses[1];

                Assert.Equal(2, blockMessage.Payloads.Length);
            }
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task GetTx()
        {
            var keyB = new PrivateKey();

            Swarm swarmA = await CreateSwarm();
            Block genesis = swarmA.Blockchain.Genesis;
            Swarm swarmB =
                await CreateSwarm(keyB, genesis: genesis);
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

            try
            {
                await StartAsync(swarmA);
                await StartAsync(swarmB);

                await swarmA.AddPeersAsync([swarmB.Peer], default);

                // List<Transaction> txs =
                //     await swarmA.GetTxsAsync(
                //         swarmB.AsPeer,
                //         new[] { tx.Id },
                //         cancellationToken: default)
                //     .ToListAsync();
                var txs = await swarmA.FetchTxAsync(swarmB.Peer, [tx.Id], default);

                Assert.Equal(new[] { tx }, txs);
            }
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task ThrowArgumentExceptionInConstructor()
        {
            var fx = new MemoryRepositoryFixture();
            var policy = new BlockchainOptions();
            var blockchain = MakeBlockChain(policy);
            var key = new PrivateKey();
            var protocol = Protocol.Create(key, 1);
            var transportOptions = new TransportOptions
            {
                Protocol = protocol,
                Host = IPAddress.Loopback.ToString(),
            };
            var transport = new NetMQTransport(key.AsSigner(), transportOptions);

            // TODO: Check Consensus Parameters.
            Assert.Throws<ArgumentNullException>(() =>
                new Swarm(null, key, transport));
            Assert.Throws<ArgumentNullException>(() =>
                new Swarm(blockchain, null, transport));
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
            Swarm s = await CreateSwarm(transportOptions: hostOptions);
            Assert.Equal(expected, s.Peer.EndPoint);
            Assert.Equal(expected, s.Peer.EndPoint);
            CleaningSwarm(s);
        }

        [Fact(Timeout = Timeout)]
        public async Task StopGracefullyWhileStarting()
        {
            Swarm a = await CreateSwarm();

            Task t = await StartAsync(a);
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
            CleaningSwarm(a);
        }

        [Fact(Timeout = Timeout)]
        public async Task AsPeer()
        {
            Swarm swarm = await CreateSwarm();
            Assert.IsType<Peer>(swarm.Peer);

            await StartAsync(swarm);
            Assert.IsType<Peer>(swarm.Peer);
            CleaningSwarm(swarm);
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
        //         await StartAsync(swarmA);
        //         await StartAsync(swarmB);

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
        //         await StartAsync(swarmA, cancellationToken: cts.Token);

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

            var miner1 = await CreateSwarm(chain1, key1);
            var miner2 = await CreateSwarm(chain2, key2);

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

            await StartAsync(miner1);
            await StartAsync(miner2);

            await BootstrapAsync(miner2, miner1.Peer);

            miner2.BroadcastBlock(latest);

            await Task.Delay(5_000);
            Assert.Equal(miner1TipHash, miner1.Blockchain.Tip.BlockHash);

            CleaningSwarm(miner1);
            CleaningSwarm(miner2);
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
                    privateKey: validKey))
;
            var swarmB = await CreateSwarm(
                MakeBlockChain(
                    policy,
                    privateKey: validKey))
;

            var invalidKey = new PrivateKey();

            try
            {
                var validTx = swarmA.Blockchain.StagedTransactions.Add(validKey);
                var invalidTx = swarmA.Blockchain.StagedTransactions.Add(invalidKey);

                await StartAsync(swarmA);
                await StartAsync(swarmB);

                await BootstrapAsync(swarmA, swarmB.Peer);

                swarmA.BroadcastTxs([validTx, invalidTx]);
                await swarmB.TxReceived.WaitAsync(default);

                Assert.Equal(swarmB.Blockchain.Transactions[validTx.Id], validTx);
                Assert.Throws<KeyNotFoundException>(
                    () => swarmB.Blockchain.Transactions[invalidTx.Id]);

                Assert.Contains(validTx.Id, swarmB.Blockchain.StagedTransactions.Keys);
                Assert.DoesNotContain(invalidTx.Id, swarmB.Blockchain.StagedTransactions.Keys);
            }
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);

                fx1.Dispose();
                fx2.Dispose();
            }
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

                await StartAsync(swarmA);
                await StartAsync(swarmB);

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
                await StartAsync(swarmA);
                await StartAsync(swarmB);
                await StartAsync(swarmC);

                await swarmB.AddPeersAsync([swarmA.Peer], default);
                await swarmC.AddPeersAsync([swarmA.Peer], default);

                var block = swarmA.Blockchain.ProposeBlock(privateKeyA);
                swarmA.Blockchain.Append(block, TestUtils.CreateBlockCommit(block));

                Task.WaitAll(
                [
                    Task.Run(() => swarmC.BlockAppended.Wait()),
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
                await StartAsync(swarmA);
                await StartAsync(swarmB);
                await StartAsync(swarmC);
                await StartAsync(swarmD);

                await swarmA.AddPeersAsync([swarmB.Peer], default);
                await swarmB.AddPeersAsync([swarmC.Peer], default);
                await swarmC.AddPeersAsync([swarmD.Peer], default);

                Peer foundPeer = await swarmA.FindSpecificPeerAsync(
                    swarmB.Peer.Address,
                    -1);

                Assert.Equal(swarmB.Peer.Address, foundPeer.Address);
                Assert.DoesNotContain(swarmC.Peer, swarmA.Peers);

                foundPeer = await swarmA.FindSpecificPeerAsync(
                    swarmD.Peer.Address,
                    -1);

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
                await StartAsync(swarmA);
                await StartAsync(swarmB);
                await StartAsync(swarmC);

                await swarmA.AddPeersAsync(new Peer[] { swarmB.Peer }, default);
                await swarmB.AddPeersAsync(new Peer[] { swarmC.Peer }, default);

                CleaningSwarm(swarmB);

                Peer foundPeer = await swarmA.FindSpecificPeerAsync(
                    swarmB.Peer.Address,
                    -1);

                Assert.Null(foundPeer);

                foundPeer = await swarmA.FindSpecificPeerAsync(
                    swarmC.Peer.Address,
                    -1);

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

            _output.WriteLine("{0}: {1}", nameof(swarmA), swarmA.Peer);
            _output.WriteLine("{0}: {1}", nameof(swarmB), swarmB.Peer);
            _output.WriteLine("{0}: {1}", nameof(swarmC), swarmC.Peer);
            _output.WriteLine("{0}: {1}", nameof(swarmD), swarmD.Peer);

            try
            {
                await StartAsync(swarmA);
                await StartAsync(swarmB);
                await StartAsync(swarmC);
                await StartAsync(swarmD);

                await swarmA.AddPeersAsync(new Peer[] { swarmB.Peer }, default);
                await swarmB.AddPeersAsync(new Peer[] { swarmC.Peer }, default);
                await swarmC.AddPeersAsync(new Peer[] { swarmD.Peer }, default);

                Peer foundPeer = await swarmA.FindSpecificPeerAsync(
                    swarmC.Peer.Address,
                    1);

                Assert.Equal(swarmC.Peer.Address, foundPeer.Address);
                swarmA.RoutingTable.Clear();
                Assert.Empty(swarmA.Peers);
                await swarmA.AddPeersAsync(new Peer[] { swarmB.Peer }, default);

                foundPeer = await swarmA.FindSpecificPeerAsync(
                    swarmD.Peer.Address,
                    1);

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
            await StartAsync(receiver);
            await StartAsync(sender);

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

                await receiver.BlockReceived.WaitAsync();
                await receiver.BlockAppended.WaitAsync();
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
            await StartAsync(receiver);
            await StartAsync(sender);

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

                await receiver.BlockReceived.WaitAsync();
                await receiver.BlockAppended.WaitAsync();
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
            await StartAsync(receiver);
            await StartAsync(sender);

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

                await receiver.BlockReceived.WaitAsync();
                await receiver.BlockAppended.WaitAsync();
                Log.Debug("Count: {Count}", receiver.Blockchain.Blocks.Count);
                sender.BroadcastBlock(sender.Blockchain.Tip);
                Assert.Equal(
                    3,
                    receiver.Blockchain.Blocks.Count);

                sender.BroadcastBlock(sender.Blockchain.Tip);

                await receiver.BlockReceived.WaitAsync();
                await receiver.BlockAppended.WaitAsync();
                Log.Debug("Count: {Count}", receiver.Blockchain.Blocks.Count);
                sender.BroadcastBlock(sender.Blockchain.Tip);
                Assert.Equal(
                    5,
                    receiver.Blockchain.Blocks.Count);

                sender.BroadcastBlock(sender.Blockchain.Tip);

                await receiver.BlockReceived.WaitAsync();
                await receiver.BlockAppended.WaitAsync();
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
                await StartAsync(swarm2);
                await StartAsync(swarm3);

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
            };
            var transportOptions = new TransportOptions
            {
                Host = "localhost",
            };

            var key = new PrivateKey();
            Swarm swarm = await CreateSwarm(
                    options: options,
                    transportOptions: transportOptions)
                .ConfigureAwait(false);
            var transport = new NetMQTransport(key.AsSigner(), transportOptions);

            try
            {
                await StartAsync(swarm);
                await transport.StartAsync(default);
                var tasks = new List<Task>();
                var content = new GetBlocksMessage { BlockHashes = [swarm.Blockchain.Genesis.BlockHash] };
                for (int i = 0; i < 5; i++)
                {
                    tasks.Add(
                        Task.Run(async () => await transport.SendMessageAsync(
                            swarm.Peer,
                            content,
                            default)));
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
            };
            var transportOptions = new TransportOptions
            {
                Host = "localhost",
            };

            var key = new PrivateKey();
            Swarm swarm = await CreateSwarm(
                    options: options,
                    transportOptions: transportOptions)
                .ConfigureAwait(false);
            NetMQTransport transport = new NetMQTransport(key.AsSigner(), transportOptions);

            try
            {
                await StartAsync(swarm);
                var fx = new MemoryRepositoryFixture();
                await transport.StartAsync(default);
                var tasks = new List<Task>();
                var content = new GetTransactionMessage { TxIds = [fx.TxId1] };
                for (int i = 0; i < 5; i++)
                {
                    tasks.Add(
                        transport.SendMessageAsync(
                            swarm.Peer,
                            content,
                            default));
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

        protected void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _logger.Debug("Starts to finalize {Resources} resources...", _finalizers.Count);
                    int i = 1;
                    foreach (Func<Task> finalize in _finalizers)
                    {
                        _logger.Debug("Tries to finalize the resource #{Resource}...", i++);
                        finalize().WaitAndUnwrapException();
                    }

                    _logger.Debug("Finished to finalize {Resources} resources", _finalizers.Count);
                    NetMQConfig.Cleanup(false);
                }

                _disposed = true;
            }
        }

        private async Task<Task> StartAsync(
            Swarm swarm,
            int millisecondsBroadcastBlockInterval = 15 * 1000,
            CancellationToken cancellationToken = default)
        {
            // await swarm.StartAsync(
            //     dialTimeout: TimeSpan.FromMilliseconds(200),
            //     broadcastBlockInterval:
            //         TimeSpan.FromMilliseconds(millisecondsBroadcastBlockInterval),
            //     broadcastTxInterval: TimeSpan.FromMilliseconds(200),
            //     cancellationToken: cancellationToken);
            throw new NotImplementedException("");
            return Task.CompletedTask;
        }

        private async Task StopAsync(Swarm swarm)
        {
            using var cts = new CancellationTokenSource(10000);
            await swarm.StopAsync(cts.Token);
        }

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
}
