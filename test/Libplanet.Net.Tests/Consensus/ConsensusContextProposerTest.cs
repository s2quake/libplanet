using Libplanet.Extensions;
using Libplanet.Net.Consensus;
using Libplanet.TestUtilities;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public class ConsensusContextProposerTest(ITestOutputHelper output)
{
    [Fact(Timeout = TestUtils.Timeout)]
    public async Task IncreaseRoundWhenTimeout()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = Rand.GetRandom(output);
        var proposer = Rand.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var transportA = CreateTransport(Signers[0]);
        await using var transportB = CreateTransport(Signers[1]);
        var options = new ConsensusServiceOptions
        {
            BlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[1], blockchain, transportB, options);
        var preVoteTimeoutTask = consensusService.TimeoutOccurred.WaitAsync(
            e => consensusService.Height == 1 && e == ConsensusStep.PreVote);
        var preCommitTimeoutTask = consensusService.TimeoutOccurred.WaitAsync(
            e => consensusService.Height == 1 && e == ConsensusStep.PreCommit);
        var roundChangedTask1 = consensusService.RoundChanged.WaitAsync(e => e.Index == 1);

        await transportA.StartAsync();
        await transportB.StartAsync();
        await consensusService.StartAsync();

        // Wait for block to be proposed.
        Assert.Equal(1, consensusService.Height);
        Assert.Equal(0, consensusService.Round);

        transportA.PostNilPreVote(transportB.Peer, validator: 2, height: 1);
        transportA.PostNilPreVote(transportB.Peer, validator: 3, height: 1);

        await preVoteTimeoutTask.WaitAsync(WaitTimeout5, cancellationToken);

        transportA.PostNilPreCommit(transportB.Peer, validator: 2, height: 1);
        transportA.PostNilPreCommit(transportB.Peer, validator: 3, height: 1);

        await preCommitTimeoutTask.WaitAsync(WaitTimeout5, cancellationToken);
        await roundChangedTask1.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(1, consensusService.Height);
        Assert.Equal(1, consensusService.Round);
    }
}
