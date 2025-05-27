using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Tests.Common;
using Libplanet;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Net.Transports;
using Libplanet.Serialization;
using Libplanet.Data;
using Libplanet.Tests.Blockchain.Evidence;
using Libplanet.Tests.Store;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Transactions;
using Serilog;
using Serilog.Events;
using xRetry;
#if NETFRAMEWORK && (NET47 || NET471)
using static Libplanet.Tests.HashSetExtensions;
#endif
using static Libplanet.Tests.LoggerExtensions;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Net.Tests
{
    public partial class SwarmTest
    {
        [Fact(Timeout = Timeout)]
        public async Task BroadcastBlock()
        {
            const int numBlocks = 5;
            var options = new BlockchainOptions();
            var genesis = new MemoryRepositoryFixture(options).GenesisBlock;

            var swarmA = await CreateSwarm(
                privateKey: new PrivateKey(),
                policy: options,
                genesis: genesis);
            var swarmB = await CreateSwarm(
                privateKey: new PrivateKey(),
                policy: options,
                genesis: genesis);
            var chainA = swarmA.BlockChain;
            var chainB = swarmB.BlockChain;

            foreach (int i in Enumerable.Range(0, numBlocks))
            {
                var block = chainA.ProposeBlock(new PrivateKey());
                chainA.Append(block, TestUtils.CreateBlockCommit(block));
            }

            Assert.Equal(numBlocks, chainA.Tip.Height);
            Assert.NotEqual(chainA.Tip, chainB.Tip);
            Assert.NotNull(chainA.BlockCommits[chainA.Tip.BlockHash]);

            try
            {
                await StartAsync(swarmA);
                await StartAsync(swarmB);

                await swarmA.AddPeersAsync(new[] { swarmB.AsPeer }, null);
                await swarmB.AddPeersAsync(new[] { swarmA.AsPeer }, null);

                swarmA.BroadcastBlock(chainA.Tip);
                await swarmB.BlockAppended.WaitAsync();

                Assert.Equal(chainA.Tip, chainB.Tip);
                Assert.Equal(
                    chainA.BlockCommits[chainA.Tip.BlockHash],
                    chainB.BlockCommits[chainB.Tip.BlockHash]);
            }
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task BroadcastBlockToReconnectedPeer()
        {
            var miner = new PrivateKey();
            var fx = new MemoryRepositoryFixture();
            var minerChain = MakeBlockChain(fx.Options);
            var policy = fx.Options;
            foreach (int i in Enumerable.Range(0, 10))
            {
                Block block = minerChain.ProposeBlock(miner);
                minerChain.Append(block, TestUtils.CreateBlockCommit(block));
            }

            Swarm seed = await CreateSwarm(
                miner,
                policy: policy,
                genesis: minerChain.Genesis);
            Blockchain seedChain = seed.BlockChain;

            var privateKey = new PrivateKey();
            Swarm swarmA = await CreateSwarm(
                privateKey: privateKey,
                policy: policy,
                genesis: minerChain.Genesis);
            Swarm swarmB = await CreateSwarm(
                privateKey: privateKey,
                policy: policy,
                genesis: minerChain.Genesis);

            foreach (BlockHash blockHash in minerChain.Blocks.Keys.Skip(1).Take(4))
            {
                seedChain.Append(
                    minerChain.Blocks[blockHash],
                    TestUtils.CreateBlockCommit(minerChain.Blocks[blockHash]));
            }

            try
            {
                await StartAsync(seed);
                await StartAsync(swarmA);
                await StartAsync(swarmB);

                Assert.Equal(swarmA.AsPeer.Address, swarmB.AsPeer.Address);
                Assert.Equal(swarmA.AsPeer.PublicIPAddress, swarmB.AsPeer.PublicIPAddress);

                await swarmA.AddPeersAsync(new[] { seed.AsPeer }, null);
                await StopAsync(swarmA);
                await seed.PeerDiscovery.RefreshTableAsync(
                    TimeSpan.Zero,
                    default);

                Assert.DoesNotContain(swarmA.AsPeer, seed.Peers);

                foreach (BlockHash blockHash in minerChain.Blocks.Keys.Skip(5))
                {
                    seedChain.Append(
                        minerChain.Blocks[blockHash],
                        TestUtils.CreateBlockCommit(minerChain.Blocks[blockHash]));
                }

                await swarmB.AddPeersAsync(new[] { seed.AsPeer }, null);

                // This is added for context switching.
                await Task.Delay(100);

                Assert.Contains(swarmB.AsPeer, seed.Peers);
                Assert.Contains(seed.AsPeer, swarmB.Peers);

                seed.BroadcastBlock(seedChain.Tip);

                await swarmB.BlockAppended.WaitAsync();

                Assert.NotEqual(seedChain.Blocks.Keys, swarmA.BlockChain.Blocks.Keys);
                Assert.Equal(seedChain.Blocks.Keys, swarmB.BlockChain.Blocks.Keys);
            }
            finally
            {
                CleaningSwarm(seed);
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task BroadcastIgnoreFromDifferentGenesisHash()
        {
            var receiverKey = new PrivateKey();
            Swarm receiverSwarm = await CreateSwarm(receiverKey);
            Blockchain receiverChain = receiverSwarm.BlockChain;
            var seedStateStore = new StateStore();
            BlockchainOptions policy = receiverChain.Options;
            Blockchain seedChain = MakeBlockChain(
                options: policy,
                privateKey: receiverKey);
            var seedMiner = new PrivateKey();
            Swarm seedSwarm =
                await CreateSwarm(seedChain, seedMiner);
            try
            {
                await StartAsync(receiverSwarm);
                await StartAsync(seedSwarm);

                await receiverSwarm.AddPeersAsync(new[] { seedSwarm.AsPeer }, null);
                Block block = seedChain.ProposeBlock(seedMiner);
                seedChain.Append(block, TestUtils.CreateBlockCommit(block));
                seedSwarm.BroadcastBlock(block);
                Assert.NotEqual(seedChain.Tip, receiverChain.Tip);
            }
            finally
            {
                CleaningSwarm(seedSwarm);
                CleaningSwarm(receiverSwarm);
            }
        }

        [RetryFact(10, Timeout = Timeout)]
        public async Task BroadcastWhileMining()
        {
            var minerA = new PrivateKey();
            var minerB = new PrivateKey();
            Swarm a = await CreateSwarm(minerA).ConfigureAwait(false);
            Swarm b = await CreateSwarm(minerB).ConfigureAwait(false);

            Blockchain chainA = a.BlockChain;
            Blockchain chainB = b.BlockChain;

            Task CreateMiner(
                PrivateKey miner,
                Swarm swarm,
                Blockchain chain,
                int delay,
                CancellationToken cancellationToken)
            {
                return Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var block = chain.ProposeBlock(miner);
                            chain.Append(block, TestUtils.CreateBlockCommit(block));

                            Log.Debug(
                                "Block mined. [Node: {0}, Block: {1}]",
                                swarm.Address,
                                block.BlockHash);
                            swarm.BroadcastBlock(block);
                        }
                        catch (OperationCanceledException)
                        {
                            continue;
                        }
                        finally
                        {
                            await Task.Delay(delay);
                        }
                    }

                    swarm.BroadcastBlock(chain.Blocks[-1]);
                    Log.Debug("Mining complete");
                });
            }

            try
            {
                await StartAsync(a);
                await StartAsync(b);

                await a.AddPeersAsync(new[] { b.AsPeer }, null);

                var minerCanceller = new CancellationTokenSource();
                Task miningA = CreateMiner(minerA, a, chainA, 4000, minerCanceller.Token);

                await Task.Delay(10000);
                minerCanceller.Cancel();

                await miningA;
                await Task.Delay(5000);
            }
            finally
            {
                CleaningSwarm(a);
                CleaningSwarm(b);
            }

            _logger.CompareBothChains(LogEventLevel.Debug, "A", chainA, "B", chainB);
            Assert.Equal(chainA.Blocks.Keys, chainB.Blocks.Keys);
        }

        [Fact(Timeout = Timeout)]
        public async Task BroadcastTx()
        {
            var minerA = new PrivateKey();
            Swarm swarmA = await CreateSwarm(minerA);
            Swarm swarmB = await CreateSwarm();
            Swarm swarmC = await CreateSwarm();

            Blockchain chainA = swarmA.BlockChain;
            Blockchain chainB = swarmB.BlockChain;
            Blockchain chainC = swarmC.BlockChain;

            var txKey = new PrivateKey();
            Transaction tx = new TransactionMetadata
            {
                Nonce = 0,
                Signer = txKey.Address,
                GenesisHash = chainA.Genesis.BlockHash,
                Actions = Array.Empty<DumbAction>().ToBytecodes(),
            }.Sign(txKey);

            chainA.StagedTransactions.Add(tx);
            Block block = chainA.ProposeBlock(minerA);
            chainA.Append(block, TestUtils.CreateBlockCommit(block));

            try
            {
                await StartAsync(swarmA);
                await StartAsync(swarmB);
                await StartAsync(swarmC);

                await swarmA.AddPeersAsync(new[] { swarmB.AsPeer }, null);
                await swarmB.AddPeersAsync(new[] { swarmC.AsPeer }, null);
                await swarmC.AddPeersAsync(new[] { swarmA.AsPeer }, null);

                swarmA.BroadcastTxs(new[] { tx });

                await swarmC.TxReceived.WaitAsync();
                await swarmB.TxReceived.WaitAsync();

                Assert.Equal(tx, chainB.Transactions[tx.Id]);
                Assert.Equal(tx, chainC.Transactions[tx.Id]);
            }
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
                CleaningSwarm(swarmC);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task BroadcastTxWhileMining()
        {
            Swarm swarmA = await CreateSwarm();
            var minerC = new PrivateKey();
            Swarm swarmC = await CreateSwarm(minerC);

            Blockchain chainA = swarmA.BlockChain;
            Blockchain chainC = swarmC.BlockChain;

            var privateKey = new PrivateKey();
            var address = privateKey.Address;
            var txCount = 10;

            var txs = Enumerable.Range(0, txCount).Select(_ =>
                    chainA.StagedTransactions.Add(new TransactionSubmission
                    {
                        Signer = new PrivateKey(),
                        Actions = [DumbAction.Create((address, "foo"))],
                    }))
                .ToArray();

            try
            {
                await StartAsync(swarmA);
                await StartAsync(swarmC);

                await swarmC.AddPeersAsync(new[] { swarmA.AsPeer }, null);
                Assert.Contains(swarmC.AsPeer, swarmA.Peers);
                Assert.Contains(swarmA.AsPeer, swarmC.Peers);

                Task miningTask = Task.Run(() =>
                {
                    for (var i = 0; i < 10; i++)
                    {
                        Block block = chainC.ProposeBlock(minerC);
                        chainC.Append(block, TestUtils.CreateBlockCommit(block));
                    }
                });

                Task txReceivedTask = swarmC.TxReceived.WaitAsync();

                for (var i = 0; i < 100; i++)
                {
                    swarmA.BroadcastTxs(txs);
                }

                await txReceivedTask;
                await miningTask;

                for (var i = 0; i < txCount; i++)
                {
                    Assert.NotNull(chainC.Transactions[txs[i].Id]);
                }
            }
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmC);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task BroadcastTxAsync()
        {
            Swarm swarmA = await CreateSwarm();
            Swarm swarmB = await CreateSwarm();
            Swarm swarmC = await CreateSwarm();

            Blockchain chainA = swarmA.BlockChain;
            Blockchain chainB = swarmB.BlockChain;
            Blockchain chainC = swarmC.BlockChain;

            var txKey = new PrivateKey();
            Transaction tx = new TransactionMetadata
            {
                Nonce = 0,
                Signer = txKey.Address,
                GenesisHash = chainA.Genesis.BlockHash,
                Actions = Array.Empty<DumbAction>().ToBytecodes(),
            }.Sign(txKey);

            chainA.StagedTransactions.Add(tx);

            try
            {
                await StartAsync(swarmA);
                await StartAsync(swarmB);
                await StartAsync(swarmC);

                // Broadcast tx swarmA to swarmB
                await swarmA.AddPeersAsync(new[] { swarmB.AsPeer }, null);

                await swarmB.TxReceived.WaitAsync();
                Assert.Equal(tx, chainB.Transactions[tx.Id]);

                CleaningSwarm(swarmA);

                // Re-Broadcast received tx swarmB to swarmC
                await swarmB.AddPeersAsync(new[] { swarmC.AsPeer }, null);

                await swarmC.TxReceived.WaitAsync();
                Assert.Equal(tx, chainC.Transactions[tx.Id]);
            }
            finally
            {
                CleaningSwarm(swarmB);
                CleaningSwarm(swarmC);
            }
        }

        [RetryFact(Timeout = Timeout)]
        public async Task BroadcastTxAsyncMany()
        {
            int size = 5;

            RepositoryFixture[] fxs = new RepositoryFixture[size];
            Blockchain[] blockChains = new Blockchain[size];
            Swarm[] swarms = new Swarm[size];

            for (int i = 0; i < size; i++)
            {
                var options = new BlockchainOptions();
                fxs[i] = new MemoryRepositoryFixture();
                blockChains[i] = new Blockchain(fxs[i].Repository, options);
                swarms[i] = await CreateSwarm(blockChains[i]).ConfigureAwait(false);
            }

            var txKey = new PrivateKey();
            Transaction tx = new TransactionMetadata
            {
                Nonce = 0,
                Signer = txKey.Address,
                GenesisHash = blockChains[size - 1].Genesis.BlockHash,
                Actions = Array.Empty<DumbAction>().ToBytecodes(),
            }.Sign(txKey);

            blockChains[size - 1].StagedTransactions.Add(tx);

            try
            {
                for (int i = 0; i < size; i++)
                {
                    await StartAsync(swarms[i]);
                }

                List<Task> tasks = new List<Task>();
                for (int i = 1; i < size; i++)
                {
                    await BootstrapAsync(swarms[i], swarms[0].AsPeer);
                }

                for (int i = 0; i < size - 1; i++)
                {
                    tasks.Add(swarms[i].TxReceived.WaitAsync());
                }

                await Task.WhenAll(tasks);

                for (int i = 0; i < size; i++)
                {
                    Assert.Equal(tx, blockChains[i].Transactions[tx.Id]);
                }
            }
            finally
            {
                for (int i = 0; i < size; i++)
                {
                    CleaningSwarm(swarms[i]);
                    fxs[i].Dispose();
                }
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task DoNotRebroadcastTxsWithLowerNonce()
        {
            // If the bucket stored peers are the same, the block may not propagate,
            // so specify private keys to make the buckets different.
            PrivateKey keyA = PrivateKey.Parse(
                "8568eb6f287afedece2c7b918471183db0451e1a61535bb0381cfdf95b85df20");
            PrivateKey keyB = PrivateKey.Parse(
                "c34f7498befcc39a14f03b37833f6c7bb78310f1243616524eda70e078b8313c");
            PrivateKey keyC = PrivateKey.Parse(
                "941bc2edfab840d79914d80fe3b30840628ac37a5d812d7f922b5d2405a223d3");

            var autoBroadcastDisabled = new SwarmOptions
            {
                BlockBroadcastInterval = TimeSpan.FromSeconds(Timeout),
                TxBroadcastInterval = TimeSpan.FromSeconds(Timeout),
            };

            var swarmA =
                await CreateSwarm(keyA, options: autoBroadcastDisabled);
            var swarmB =
                await CreateSwarm(keyB, options: autoBroadcastDisabled);
            var swarmC =
                await CreateSwarm(keyC, options: autoBroadcastDisabled);

            Blockchain chainA = swarmA.BlockChain;
            Blockchain chainB = swarmB.BlockChain;
            Blockchain chainC = swarmC.BlockChain;

            var privateKey = new PrivateKey();

            try
            {
                var tx1 = swarmA.BlockChain.StagedTransactions.Add(submission: new()
                {
                    Signer = privateKey,
                });
                var tx2 = swarmA.BlockChain.StagedTransactions.Add(submission: new()
                {
                    Signer = privateKey,
                });
                Assert.Equal(0, tx1.Nonce);
                Assert.Equal(1, tx2.Nonce);

                await StartAsync(swarmA);
                await StartAsync(swarmB);
                await swarmA.AddPeersAsync(new[] { swarmB.AsPeer }, null);
                swarmA.BroadcastTxs(new[] { tx1, tx2 });
                await swarmB.TxReceived.WaitAsync();
                Assert.Equal(
                    new HashSet<TxId> { tx1.Id, tx2.Id },
                    chainB.StagedTransactions.Keys.ToHashSet());
                swarmA.RoutingTable.RemovePeer(swarmB.AsPeer);
                swarmB.RoutingTable.RemovePeer(swarmA.AsPeer);

                chainA.StagedTransactions.Remove(tx2.Id);
                Assert.Equal(1, chainA.GetNextTxNonce(privateKey.Address));

                await StopAsync(swarmA);
                await StopAsync(swarmB);

                swarmA.RoutingTable.RemovePeer(swarmB.AsPeer);
                swarmB.RoutingTable.RemovePeer(swarmA.AsPeer);
                Assert.Empty(swarmA.Peers);
                Assert.Empty(swarmB.Peers);

                await StartAsync(swarmA);
                await StartAsync(swarmB);

                Block block = chainB.ProposeBlock(keyB);
                chainB.Append(block, TestUtils.CreateBlockCommit(block));

                var tx3 = chainA.StagedTransactions.Add(submission: new()
                {
                    Signer = privateKey,
                });
                var tx4 = chainA.StagedTransactions.Add(submission: new()
                {
                    Signer = privateKey,
                });
                Assert.Equal(1, tx3.Nonce);
                Assert.Equal(2, tx4.Nonce);

                await StartAsync(swarmC);
                await swarmA.AddPeersAsync(new[] { swarmB.AsPeer }, null);
                await swarmB.AddPeersAsync(new[] { swarmC.AsPeer }, null);

                swarmA.BroadcastTxs(new[] { tx3, tx4 });
                await swarmB.TxReceived.WaitAsync();

                // SwarmB receives tx3 and is staged, but policy filters it.
                Assert.DoesNotContain(tx3.Id, chainB.StagedTransactions.Keys);
                Assert.Contains(
                    tx3.Id,
                    chainB.StagedTransactions.Keys);
                Assert.Contains(tx4.Id, chainB.StagedTransactions.Keys);

                await swarmC.TxReceived.WaitAsync();
                // SwarmC can not receive tx3 because SwarmB does not rebroadcast it.
                Assert.DoesNotContain(tx3.Id, chainC.StagedTransactions.Keys);
                Assert.DoesNotContain(
                    tx3.Id,
                    chainC.StagedTransactions.Keys);
                Assert.Contains(tx4.Id, chainC.StagedTransactions.Keys);
            }
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
                CleaningSwarm(swarmC);
            }
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

            Blockchain chainA = swarmA.BlockChain;
            Blockchain chainB = swarmB.BlockChain;
            Blockchain chainC = swarmC.BlockChain;

            foreach (int i in Enumerable.Range(0, 10))
            {
                Block block = chainA.ProposeBlock(keyA);
                chainA.Append(block, TestUtils.CreateBlockCommit(block));
                if (i < 5)
                {
                    chainB.Append(block, TestUtils.CreateBlockCommit(block));
                }
            }

            try
            {
                await StartAsync(swarmA);
                await StartAsync(swarmB);
                await StartAsync(swarmC);

                await BootstrapAsync(swarmB, swarmA.AsPeer);
                await BootstrapAsync(swarmC, swarmA.AsPeer);

                swarmB.BroadcastBlock(chainB.Tip);

                // chainA ignores block header received because its index is shorter.
                await swarmA.BlockHeaderReceived.WaitAsync();
                await swarmC.BlockAppended.WaitAsync();
                Assert.False(swarmA.BlockAppended.IsSet);

                // chainB doesn't applied to chainA since chainB is shorter
                // than chainA
                Assert.NotEqual(chainB, chainA);

                swarmA.BroadcastBlock(chainA.Tip);

                await swarmB.BlockAppended.WaitAsync();
                await swarmC.BlockAppended.WaitAsync();

                Log.Debug("Compare chainA and chainB");
                Assert.Equal(chainA.Blocks.Keys, chainB.Blocks.Keys);
                Log.Debug("Compare chainA and chainC");
                Assert.Equal(chainA.Blocks.Keys, chainC.Blocks.Keys);
            }
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
                CleaningSwarm(swarmC);
            }
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
            var blockChain = MakeBlockChain(options);
            var privateKey = new PrivateKey();
            var minerSwarm = await CreateSwarm(blockChain, privateKey);
            var fx2 = new MemoryRepositoryFixture();
            // var receiverRenderer = new RecordingActionRenderer();
            // var loggedRenderer = new LoggedActionRenderer(
            //     receiverRenderer,
            //     _logger);
            var receiverChain = MakeBlockChain(options);
            Swarm receiverSwarm = await CreateSwarm(receiverChain);

            int renderCount = 0;

            // receiverRenderer.RenderEventHandler += (_, a) => renderCount += IsDumbAction(a) ? 1 : 0;

            Transaction[] transactions =
            {
                fx1.MakeTransaction(
                    new[]
                    {
                        DumbAction.Create((fx1.Address2, "foo")),
                        DumbAction.Create((fx1.Address2, "bar")),
                    },
                    timestamp: DateTimeOffset.MinValue,
                    nonce: 0,
                    privateKey: privateKey),
                fx1.MakeTransaction(
                    new[]
                    {
                        DumbAction.Create((fx1.Address2, "baz")),
                        DumbAction.Create((fx1.Address2, "qux")),
                    },
                    timestamp: DateTimeOffset.MinValue.AddSeconds(5),
                    nonce: 1,
                    privateKey: privateKey),
            };

            Block block1 = blockChain.ProposeBlock(GenesisProposer);
            blockChain.Append(block1, TestUtils.CreateBlockCommit(block1));
            Block block2 = blockChain.ProposeBlock(GenesisProposer);
            blockChain.Append(block2, TestUtils.CreateBlockCommit(block2));

            try
            {
                await StartAsync(minerSwarm);
                await StartAsync(receiverSwarm);

                await BootstrapAsync(receiverSwarm, minerSwarm.AsPeer);

                minerSwarm.BroadcastBlock(block2);

                await AssertThatEventually(
                    () => receiverChain.Tip.Equals(block2),
                    5_000,
                    1_000);
                Assert.Equal(3, receiverChain.Blocks.Count);
                Assert.Equal(4, renderCount);
            }
            finally
            {
                CleaningSwarm(minerSwarm);
                CleaningSwarm(receiverSwarm);
                fx1.Dispose();
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task BroadcastBlockWithoutGenesis()
        {
            var keyA = new PrivateKey();
            var keyB = new PrivateKey();

            Swarm swarmA = await CreateSwarm(keyA);
            Swarm swarmB = await CreateSwarm(keyB);

            Blockchain chainA = swarmA.BlockChain;
            Blockchain chainB = swarmB.BlockChain;

            try
            {
                await StartAsync(swarmA);
                await StartAsync(swarmB);

                await BootstrapAsync(swarmB, swarmA.AsPeer);
                var block = chainA.ProposeBlock(keyA);
                chainA.Append(block, TestUtils.CreateBlockCommit(block));
                swarmA.BroadcastBlock(chainA.Blocks[-1]);

                await swarmB.BlockAppended.WaitAsync();

                Assert.Equal(chainB.Blocks.Keys, chainA.Blocks.Keys);

                block = chainA.ProposeBlock(keyB);
                chainA.Append(block, TestUtils.CreateBlockCommit(block));
                swarmA.BroadcastBlock(chainA.Blocks[-1]);

                await swarmB.BlockAppended.WaitAsync();

                Assert.Equal(chainB.Blocks.Keys, chainA.Blocks.Keys);
            }
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task IgnoreExistingBlocks()
        {
            var keyA = new PrivateKey();
            var keyB = new PrivateKey();

            Swarm swarmA = await CreateSwarm(keyA);
            Swarm swarmB =
                await CreateSwarm(keyB, genesis: swarmA.BlockChain.Genesis);

            Blockchain chainA = swarmA.BlockChain;
            Blockchain chainB = swarmB.BlockChain;

            var block = chainA.ProposeBlock(keyA);
            BlockCommit blockCommit = TestUtils.CreateBlockCommit(block);
            chainA.Append(block, blockCommit);
            chainB.Append(block, blockCommit);

            foreach (int i in Enumerable.Range(0, 3))
            {
                block = chainA.ProposeBlock(keyA);
                chainA.Append(block, TestUtils.CreateBlockCommit(block));
            }

            try
            {
                await StartAsync(swarmA);
                await StartAsync(swarmB);

                await BootstrapAsync(swarmB, swarmA.AsPeer);
                swarmA.BroadcastBlock(chainA.Blocks[-1]);
                await swarmB.BlockAppended.WaitAsync();

                Assert.Equal(chainA.Blocks.Keys, chainB.Blocks.Keys);

                CancellationTokenSource cts = new CancellationTokenSource();
                swarmA.BroadcastBlock(chainA.Blocks[-1]);
                Task t = swarmB.BlockAppended.WaitAsync(cts.Token);

                // Actually, previous code may pass this test if message is
                // delayed over 5 seconds.
                await Task.Delay(5000);
                Assert.False(t.IsCompleted);

                cts.Cancel();
            }
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task PullBlocks()
        {
            var keyA = new PrivateKey();
            var keyB = new PrivateKey();
            var keyC = new PrivateKey();

            var swarmA = await CreateSwarm(keyA);
            var swarmB = await CreateSwarm(keyB);
            var swarmC = await CreateSwarm(keyC);

            Blockchain chainA = swarmA.BlockChain;
            Blockchain chainB = swarmB.BlockChain;
            Blockchain chainC = swarmC.BlockChain;

            foreach (int i in Enumerable.Range(0, 5))
            {
                Block block = chainA.ProposeBlock(keyA);
                chainA.Append(block, TestUtils.CreateBlockCommit(block));
                if (i < 3)
                {
                    chainC.Append(block, TestUtils.CreateBlockCommit(block));
                }
            }

            Block chainATip = chainA.Tip;

            foreach (int i in Enumerable.Range(0, 10))
            {
                Block block = chainB.ProposeBlock(keyB);
                chainB.Append(block, TestUtils.CreateBlockCommit(block));
            }

            try
            {
                await StartAsync(swarmA);
                await StartAsync(swarmB);
                await StartAsync(swarmC);

                await BootstrapAsync(swarmB, swarmA.AsPeer);
                await BootstrapAsync(swarmC, swarmA.AsPeer);

                await swarmC.PullBlocksAsync(TimeSpan.FromSeconds(5), int.MaxValue, default);
                await swarmC.BlockAppended.WaitAsync();
                Assert.Equal(chainC.Tip, chainATip);
            }
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
                CleaningSwarm(swarmC);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task CanFillWithInvalidTransaction()
        {
            var privateKey = new PrivateKey();
            var address = privateKey.Address;
            var swarm1 = await CreateSwarm();
            var swarm2 = await CreateSwarm();

            var tx1 = swarm2.BlockChain.StagedTransactions.Add(submission: new()
            {
                Signer = privateKey,
                Actions = [DumbAction.Create((address, "foo"))],
            });

            var tx2 = swarm2.BlockChain.StagedTransactions.Add(submission: new()
            {
                Signer = privateKey,
                Actions = [DumbAction.Create((address, "bar"))],
            });

            var tx3 = swarm2.BlockChain.StagedTransactions.Add(submission: new()
            {
                Signer = privateKey,
                Actions = [DumbAction.Create((address, "quz"))],
            });

            var tx4 = new TransactionMetadata
            {
                Nonce = 4,
                Signer = privateKey.Address,
                GenesisHash = swarm1.BlockChain.Genesis.BlockHash,
                Actions = new[] { DumbAction.Create((address, "qux")) }.ToBytecodes(),
            }.Sign(privateKey);

            try
            {
                await StartAsync(swarm1);
                await StartAsync(swarm2);
                await swarm1.AddPeersAsync(new[] { swarm2.AsPeer }, null);

                var transport = swarm1.Transport;
                var msg = new GetTransactionMessage { TxIds = [tx1.Id, tx2.Id, tx3.Id, tx4.Id] };
                var replies = (await transport.SendMessageAsync(
                    swarm2.AsPeer,
                    msg,
                    TimeSpan.FromSeconds(1),
                    4,
                    true,
                    default)).ToList();

                Assert.Equal(3, replies.Count);
                Assert.Equal(
                    new[] { tx1, tx2, tx3 }.ToHashSet(),
                    replies.Select(
                        m => ModelSerializer.DeserializeFromBytes<Transaction>(
                            ((TransactionMessage)m.Content).Payload)).ToHashSet());
            }
            finally
            {
                CleaningSwarm(swarm1);
                CleaningSwarm(swarm2);
            }
        }

        [Fact(Timeout = Timeout)]
        public async Task DoNotSpawnMultipleTaskForSinglePeer()
        {
            var key = new PrivateKey();
            var apv = new AppProtocolVersionOptions();
            Swarm receiver =
                await CreateSwarm(appProtocolVersionOptions: apv);
            ITransport mockTransport = await NetMQTransport.Create(
                new PrivateKey(),
                apv,
                new HostOptions(
                    IPAddress.Loopback.ToString(),
                    Array.Empty<IceServer>()));
            int requestCount = 0;

            async Task MessageHandler(Message message)
            {
                _logger.Debug("Received message: {Content}", message);
                switch (message.Content)
                {
                    case PingMessage ping:
                        await mockTransport.ReplyMessageAsync(
                            new PongMessage(),
                            message.Identity,
                            default);
                        break;

                    case GetBlockHashesMessage gbhm:
                        requestCount++;
                        break;
                }
            }

            mockTransport.ProcessMessageHandler.Register(MessageHandler);

            Block block1 = ProposeNextBlock(
                receiver.BlockChain.Genesis,
                key,
                []);
            Block block2 = ProposeNextBlock(
                block1,
                key,
                []);

            try
            {
                await StartAsync(receiver);
                _ = mockTransport.StartAsync();
                await mockTransport.WaitForRunningAsync();

                // Send block header for block 1.
                var blockHeaderMsg1 = new BlockHeaderMessage
                {
                    GenesisHash = receiver.BlockChain.Genesis.BlockHash,
                    Excerpt = block1
                };
                await mockTransport.SendMessageAsync(
                    receiver.AsPeer,
                    blockHeaderMsg1,
                    TimeSpan.FromSeconds(5),
                    default);
                await receiver.BlockHeaderReceived.WaitAsync();

                // Wait until FillBlockAsync task has spawned block demand task.
                await Task.Delay(1000);

                // Re-send block header for block 1, make sure it does not spawn new task.
                await mockTransport.SendMessageAsync(
                    receiver.AsPeer,
                    blockHeaderMsg1,
                    TimeSpan.FromSeconds(5),
                    default);
                await receiver.BlockHeaderReceived.WaitAsync();
                await Task.Delay(1000);

                // Send block header for block 2, make sure it does not spawn new task.
                var blockHeaderMsg2 = new BlockHeaderMessage
                {
                    GenesisHash = receiver.BlockChain.Genesis.BlockHash,
                    Excerpt = block2
                };
                await mockTransport.SendMessageAsync(
                    receiver.AsPeer,
                    blockHeaderMsg2,
                    TimeSpan.FromSeconds(5),
                    default);
                await receiver.BlockHeaderReceived.WaitAsync();
                await Task.Delay(1000);

                Assert.Equal(1, requestCount);
            }
            finally
            {
                CleaningSwarm(receiver);
                await mockTransport.StopAsync(TimeSpan.FromMilliseconds(10));
                mockTransport.Dispose();
            }
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

            var chainA = swarmA.BlockChain;
            var chainB = swarmB.BlockChain;
            var chainC = swarmC.BlockChain;

            var evidence = TestEvidence.Create(0, validatorAddress, DateTimeOffset.UtcNow);
            chainA.PendingEvidences.Add(evidence);

            try
            {
                await StartAsync(swarmA);
                await StartAsync(swarmB);
                await StartAsync(swarmC);

                await swarmA.AddPeersAsync(new[] { swarmB.AsPeer }, null);
                await swarmB.AddPeersAsync(new[] { swarmC.AsPeer }, null);
                await swarmC.AddPeersAsync(new[] { swarmA.AsPeer }, null);

                swarmA.BroadcastEvidence(new[] { evidence });

                await swarmC.EvidenceReceived.WaitAsync(cancellationTokenSource.Token);
                await swarmB.EvidenceReceived.WaitAsync(cancellationTokenSource.Token);

                Assert.Equal(evidence, chainB.PendingEvidences[evidence.Id]);
                Assert.Equal(evidence, chainC.PendingEvidences[evidence.Id]);
            }
            finally
            {
                CleaningSwarm(swarmA);
                CleaningSwarm(swarmB);
                CleaningSwarm(swarmC);
            }
        }
    }
}
