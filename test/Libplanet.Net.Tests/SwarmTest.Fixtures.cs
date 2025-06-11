using System.Net;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Options;
using Libplanet.Net.Transports;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Serilog;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Net.Tests
{
    public partial class SwarmTest
    {
        private static Block[] _fixtureBlocksForPreloadAsyncCancellationTest;

        private readonly List<Func<Task>> _finalizers;

        private static (Address, Block[])
            MakeFixtureBlocksForPreloadAsyncCancellationTest()
        {
            Block[] blocks = _fixtureBlocksForPreloadAsyncCancellationTest;

            if (blocks is null)
            {
                var policy = new BlockchainOptions
                {
                    SystemActions = new SystemActions
                    {
                        EndBlockActions = [new MinerReward(1)],
                    },
                };
                using (var storeFx = new MemoryRepositoryFixture())
                {
                    var chain = MakeBlockChain(policy);
                    var miner = new PrivateKey();
                    var signer = new PrivateKey();
                    Address address = signer.Address;
                    Log.Logger.Information("Fixture blocks:");
                    for (int i = 0; i < 20; i++)
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            chain.StagedTransactions.Add(submission: new()
                            {
                                Signer = signer,
                                Actions = [DumbAction.Create((address, $"Item{i}.{j}"))],
                            });
                        }

                        Block block = chain.ProposeBlock(miner);
                        Log.Logger.Information("  #{0,2} {1}", block.Height, block.BlockHash);
                        chain.Append(block, CreateBlockCommit(block));
                    }

                    var blockList = new List<Block>();
                    for (var i = 1; i < chain.Blocks.Count; i++)
                    {
                        Block block = chain.Blocks[i];
                        blockList.Add(block);
                    }

                    blocks = blockList.ToArray();

                    _fixtureBlocksForPreloadAsyncCancellationTest = blocks;
                }
            }

            var action = blocks[1].Transactions.First().Actions.FromImmutableBytes().OfType<DumbAction>().First();
            return (action.Append is { } s ? s.At : throw new NullReferenceException(), blocks);
        }

        private Task<Swarm> CreateConsensusSwarm(
            PrivateKey? privateKey = null,
            ProtocolOptions? appProtocolVersionOptions = null,
            HostOptions? hostOptions = null,
            SwarmOptions? options = null,
            BlockchainOptions? policy = null,
            Block? genesis = null,
            ConsensusReactorOption? consensusReactorOption = null)
        {
            return CreateSwarm(
                privateKey,
                appProtocolVersionOptions,
                hostOptions,
                options,
                policy,
                genesis,
                consensusReactorOption ?? new ConsensusReactorOption
                {
                    SeedPeers = ImmutableList<Peer>.Empty,
                    ConsensusPeers = ImmutableList<Peer>.Empty,
                    ConsensusPort = 0,
                    ConsensusPrivateKey = new PrivateKey(),
                    ConsensusWorkers = 100,
                    TargetBlockInterval = TimeSpan.FromSeconds(10),
                });
        }

        private async Task<Swarm> CreateSwarm(
            PrivateKey? privateKey = null,
            ProtocolOptions? appProtocolVersionOptions = null,
            HostOptions? hostOptions = null,
            SwarmOptions? options = null,
            BlockchainOptions? policy = null,
            Block? genesis = null,
            ConsensusReactorOption? consensusReactorOption = null)
        {
            policy ??= new BlockchainOptions
            {
                SystemActions = new SystemActions
                {
                    EndBlockActions = [new MinerReward(1)],
                },
            };
            var fx = new MemoryRepositoryFixture(policy);
            var blockchain = MakeBlockChain(policy, genesisBlock: genesis);
            appProtocolVersionOptions ??= new ProtocolOptions();
            hostOptions ??= new HostOptions
            {
                Host = IPAddress.Loopback.ToString(),
            };

            return await CreateSwarm(
                blockchain,
                privateKey,
                appProtocolVersionOptions,
                hostOptions,
                options,
                consensusReactorOption: consensusReactorOption);
        }

        private async Task<Swarm> CreateSwarm(
            Blockchain blockChain,
            PrivateKey? privateKey = null,
            ProtocolOptions? appProtocolVersionOptions = null,
            HostOptions? hostOptions = null,
            SwarmOptions? options = null,
            ConsensusReactorOption? consensusReactorOption = null)
        {
            appProtocolVersionOptions ??= new ProtocolOptions();
            hostOptions ??= new HostOptions
            {
                Host = IPAddress.Loopback.ToString(),
            };
            options ??= new SwarmOptions();
            privateKey ??= new PrivateKey();
            var transport = await NetMQTransport.Create(
                privateKey,
                appProtocolVersionOptions,
                hostOptions,
                options.MessageTimestampBuffer);
            ITransport consensusTransport = null;
            if (consensusReactorOption is { } option)
            {
                var consensusHostOptions = new HostOptions
                {
                    Host = hostOptions.Host,
                    Port = option.ConsensusPort
                };
                consensusTransport = await NetMQTransport.Create(
                    privateKey,
                    appProtocolVersionOptions,
                    consensusHostOptions,
                    options.MessageTimestampBuffer);
            }

            var swarm = new Swarm(
                blockChain,
                privateKey,
                transport,
                options,
                consensusTransport: consensusTransport,
                consensusOption: consensusReactorOption);
            _finalizers.Add(async () =>
            {
                try
                {
                    await StopAsync(swarm);
                    swarm.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    _logger.Debug("Swarm {Swarm} is already disposed", swarm);
                }
            });
            return swarm;
        }
    }
}
