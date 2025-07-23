using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Data;
using Libplanet.Tests.Store;

namespace Libplanet.Net.Tests.Consensus;

[Collection("NetMQConfiguration")]
public class ConsensusReactorTest
{
    private const int PropagationDelay = 25_000;
    private const int Timeout = 60 * 1000;

    [Fact(Timeout = Timeout)]
    public async Task StartAsync()
    {
        var count = TestUtils.PrivateKeys.Count;
        var consensusServices = new ConsensusService[count];
        var blockchains = new Blockchain[count];
        using var fx = new MemoryRepositoryFixture();
        var validatorPeers = new List<Peer>();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        for (var i = 0; i < count; i++)
        {
            var peer = new Peer
            {
                Address = TestUtils.PrivateKeys[i].Address,
                EndPoint = new DnsEndPoint("127.0.0.1", 6000 + i),
            };
            validatorPeers.Add(peer);
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
            consensusServices[i] = TestUtils.CreateConsensusService(
                blockchain: blockchains[i],
                key: TestUtils.PrivateKeys[i],
                port: 6000 + i,
                validatorPeers: [.. validatorPeers],
                newHeightDelay: TimeSpan.FromMilliseconds(PropagationDelay * 2));
        }

        try
        {
            consensusServices.AsParallel().ForAll(
                reactor => _ = reactor.StartAsync(cancellationToken));

            await Task.Delay(PropagationDelay, cancellationToken);
            await Parallel.ForEachAsync(
                consensusServices,
                cancellationToken,
                async (consensusService, cancellationToken) => await consensusService.StopAsync(cancellationToken));

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
        finally
        {
            await cancellationTokenSource.CancelAsync();
            for (var i = 0; i < 4; ++i)
            {
                await consensusServices[i].DisposeAsync();
            }
        }
    }
}
