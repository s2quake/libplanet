using System.Threading.Tasks;
using Libplanet.Extensions;
using Libplanet.Net.Services;
using Libplanet.Tests;
using Libplanet.Tests.Store;
using Libplanet.TestUtilities;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Services;

public partial class BlockchainSynchronizationServiceTest(ITestOutputHelper output)
{
    private const int Timeout = 60 * 1000;

    [Fact(Timeout = Timeout)]
    public async Task SynchronizeAsync()
    {
        // Given
        var random = RandomUtility.GetRandom(output);
        using var fx = new MemoryRepositoryFixture();
        var keyA = RandomUtility.PrivateKey(random);
        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var blockchainA = TestUtils.CreateBlockchain(genesisBlock: fx.GenesisBlock);
        var blockchainB = TestUtils.CreateBlockchain(genesisBlock: fx.GenesisBlock);
        var serviceA = new BlockchainSynchronizationResponderService(blockchainA, transportA);
        var serviceB = new BlockchainSynchronizationService(blockchainB, transportB);
        await using var services = new ServiceCollection
        {
            transportA,
            transportB,
            serviceA,
            serviceB,
        };

        blockchainA.ProposeAndAppendMany(keyA, 10);
        await services.StartAsync(default);

        // When
        transportA.PostBlock(transportB.Peer, blockchainA, blockchainA.Tip);

        // Then
        await serviceB.Synchronized.WaitAsync();
        Assert.Equal(blockchainA.Tip, blockchainB.Tip);
        Assert.Equal(0, serviceB.BlockBranches.Count);
        Assert.Equal(0, serviceB.BlockDemands.Count);
    }
}
