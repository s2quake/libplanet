using System.Net;
using System.Threading.Tasks;
using Libplanet.State;
using Libplanet.State.Tests.Actions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Options;
using Libplanet.Net.NetMQ;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Tests.Store;
using Libplanet.Types;
using Serilog;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Net.Tests;

public partial class SwarmTest
{
    private static Block[] _fixtureBlocksForPreloadAsyncCancellationTest;

    // private readonly List<Func<Task>> _finalizers;

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
                var chain = MakeBlockchain(policy);
                var miner = new PrivateKey();
                var signer = new PrivateKey();
                Address address = signer.Address;
                Log.Logger.Information("Fixture blocks:");
                for (int i = 0; i < 20; i++)
                {
                    for (int j = 0; j < 5; j++)
                    {
                        chain.StagedTransactions.Add(signer, submission: new()
                        {
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
        SwarmOptions? options = null,
        BlockchainOptions? policy = null,
        Block? genesis = null,
        ConsensusReactorOptions? consensusReactorOption = null)
    {
        return CreateSwarm(
            privateKey,
            options,
            policy,
            genesis,
            consensusReactorOption ?? new ConsensusReactorOptions
            {
                Seeds = [],
                Validators = [],
                Workers = 100,
                TargetBlockInterval = TimeSpan.FromSeconds(10),
            });
    }

    private async Task<Swarm> CreateSwarm(
        PrivateKey? privateKey = null,
        SwarmOptions? options = null,
        BlockchainOptions? policy = null,
        Block? genesis = null,
        ConsensusReactorOptions? consensusReactorOption = null)
    {
        policy ??= new BlockchainOptions
        {
            SystemActions = new SystemActions
            {
                EndBlockActions = [new MinerReward(1)],
            },
        };
        var fx = new MemoryRepositoryFixture(policy);
        var blockchain = MakeBlockchain(policy, genesisBlock: genesis);

        return await CreateSwarm(
            blockchain,
            privateKey,
            options,
            consensusReactorOption: consensusReactorOption);
    }

    private async Task<Swarm> CreateSwarm(
        Blockchain blockchain,
        PrivateKey? privateKey = null,
        // TransportOptions? transportOptions = null,
        SwarmOptions? options = null,
        ConsensusReactorOptions? consensusReactorOption = null)
    {
        options ??= new SwarmOptions();
        privateKey ??= new PrivateKey();
        // transportOptions ??= new TransportOptions();
        // var transport = new NetMQTransport(privateKey.AsSigner(), transportOptions ?? new TransportOptions());
        ITransport consensusTransport = null;
        if (consensusReactorOption is { } option)
        {
            // var consensusHostOptions = transportOptions with { Port = option.Port };
            // consensusTransport = new NetMQTransport(privateKey.AsSigner(), consensusHostOptions);
        }

        var swarm = new Swarm(
            privateKey.AsSigner(),
            blockchain,
            options,
            consensusOption: consensusReactorOption);
        // _finalizers.Add(async () =>
        // {
        //     try
        //     {
        //         await StopAsync(swarm);
        //         await swarm.DisposeAsync();
        //     }
        //     catch (ObjectDisposedException)
        //     {
        //         // logging
        //     }
        // });
        return swarm;
    }
}
