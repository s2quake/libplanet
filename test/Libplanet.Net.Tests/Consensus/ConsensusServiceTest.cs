using System.Net;
using Libplanet.Net.Consensus;
using Libplanet.Data;
using Libplanet.Tests.Store;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public class ConsensusServiceTest
{
    private const int PropagationDelay = 25_000;

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task StartAsync()
    {
        var count = Signers.Length;
        var blockchains = new Blockchain[count];
        using var fx = new MemoryRepositoryFixture();
        var validatorPeers = new List<Peer>();
        await using ServiceCollection<ITransport> transports = [];
        await using ServiceCollection<ConsensusService> consensusServices = [];

        for (var i = 0; i < count; i++)
        {
            var transport = CreateTransport(Signers[i]);
            transports.Add(transport);
            validatorPeers.Add(transport.Peer);
        }

        for (var i = 0; i < count; i++)
        {
            var blockchainOptions = TestUtils.BlockchainOptions with
            {
            };
            var repository = new Repository();
            blockchains[i] = new Blockchain(fx.GenesisBlock, repository, blockchainOptions);
        }

        for (var i = 0; i < count; i++)
        {
            var options = new ConsensusServiceOptions
            {
                Validators = [.. validatorPeers.Except([transports[i].Peer])],
                TargetBlockInterval = TimeSpan.FromMilliseconds(PropagationDelay * 2),
            };

            consensusServices.Add(new ConsensusService(Signers[i], blockchains[i], transports[i], options));
        }

        await transports.StartAsync();
        await consensusServices.StartAsync();

        await Task.Delay(PropagationDelay, default);
        await consensusServices.StopAsync();

        var isPolka = new bool[4];
        for (var i = 0; i < 4; ++i)
        {
            // Genesis block exists, add 1 to the height.
            if (consensusServices[i].Step == ConsensusStep.EndCommit)
            {
                isPolka[i] = true;
            }
            else
            {
                isPolka[i] = false;
            }
        }

        Assert.Equal(4, isPolka.Sum(x => x ? 1 : 0));

        for (var i = 0; i < 4; ++i)
        {
            var consensusService = consensusServices[i];
            Assert.Equal(validatorPeers[i].Address, consensusService.Address);
            Assert.Equal(1, consensusService.Height);
            Assert.Equal(2, blockchains[i].Blocks.Count);
            Assert.Equal(0, consensusService.Round);
            Assert.Equal(ConsensusStep.EndCommit, consensusService.Step);
        }
    }
}
