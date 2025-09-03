using Libplanet.Extensions;
using Libplanet.Net.Services;
using Libplanet.Tests;
using Libplanet.TestUtilities;

namespace Libplanet.Net.Tests.Services;

public partial class BlockchainSynchronizationServiceTest(ITestOutputHelper output)
{
    private const int Timeout = 60 * 1000;

    [Fact(Timeout = Timeout)]
    public async Task SynchronizeAsync()
    {
        // Given
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var signerA = RandomUtility.Signer(random);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);

        var transportA = TestUtils.CreateTransport();
        var transportB = TestUtils.CreateTransport();
        var blockchainA = new Blockchain(genesisBlock);
        var blockchainB = new Blockchain(genesisBlock);
        var serviceA = new BlockSynchronizationResponderService(blockchainA, transportA);
        var serviceB = new BlockSynchronizationService(blockchainB, transportB);
        await using var services = new ServiceCollection
        {
            transportA,
            transportB,
            serviceA,
            serviceB,
        };

        blockchainA.ProposeAndAppendMany(signerA, 10);
        await services.StartAsync(cancellationToken);

        // When
        transportA.PostBlock(transportB.Peer, blockchainA, blockchainA.Tip);

        // Then
        await serviceB.Appended.WaitAsync();
        Assert.Equal(blockchainA.Tip, blockchainB.Tip);
        Assert.Equal(0, serviceB.BlockBranches.Count);
        Assert.Equal(0, serviceB.BlockDemands.Count);
    }
}
