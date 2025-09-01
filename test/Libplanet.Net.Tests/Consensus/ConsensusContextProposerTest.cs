using Libplanet.Net.Messages;
using Libplanet.Types;
using static Libplanet.Net.Tests.TestUtils;
using Libplanet.Net.Consensus;
using Libplanet.Extensions;
using Libplanet.TestUtilities;
using Libplanet.Tests;

namespace Libplanet.Net.Tests.Consensus;

public class ConsensusContextProposerTest(ITestOutputHelper output)
{
    [Fact(Timeout = TestUtils.Timeout)]
    public async Task IncreaseRoundWhenTimeout()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = new GenesisBlockBuilder
        {
        }.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var transportA = CreateTransport(Signers[0]);
        await using var transportB = CreateTransport(Signers[1]);
        var options = new ConsensusServiceOptions
        {
            TargetBlockInterval = TimeSpan.FromSeconds(1),
        };
        await using var consensusService = new ConsensusService(Signers[1], blockchain, transportB, options);
        var preVoteTimeoutTask = consensusService.TimeoutOccurred.WaitAsync(
            e => consensusService.Height == 1 && e == ConsensusStep.PreVote);
        var preCommitTimeoutTask = consensusService.TimeoutOccurred.WaitAsync(
            e => consensusService.Height == 1 && e == ConsensusStep.PreCommit);

        await transportA.StartAsync();
        await transportB.StartAsync();
        await consensusService.StartAsync();

        // Wait for block to be proposed.
        Assert.Equal(1, consensusService.Height);
        Assert.Equal(0, consensusService.Round);

        // Triggers timeout +2/3 with NIL and Block
        var preVote2 = new NilVoteBuilder
        {
            Validator = Validators[2],
            Height = 1,
            Type = VoteType.PreVote,
        }.Create(Signers[2]);
        var preVoteMessage2 = new ConsensusPreVoteMessage
        {
            PreVote = preVote2
        };
        transportA.Post(transportB.Peer, preVoteMessage2);

        var preVote3 = new NilVoteBuilder
        {
            Validator = Validators[3],
            Height = 1,
            Type = VoteType.PreVote,
        }.Create(Signers[3]);
        var preVoteMessage3 = new ConsensusPreVoteMessage
        {
            PreVote = preVote3
        };
        transportA.Post(transportB.Peer, preVoteMessage3);

        await preVoteTimeoutTask.WaitAsync(WaitTimeout5, cancellationToken);

        var preCommit2 = new NilVoteBuilder
        {
            Validator = Validators[2],
            Height = 1,
            Type = VoteType.PreCommit,
        }.Create(Signers[2]);
        var preCommitMessage2 = new ConsensusPreCommitMessage
        {
            PreCommit = preCommit2
        };
        transportA.Post(transportB.Peer, preCommitMessage2);

        var preCommit3 = new NilVoteBuilder
        {
            Validator = Validators[3],
            Height = 1,
            Type = VoteType.PreCommit,
        }.Create(Signers[3]);
        var preCommitMessage3 = new ConsensusPreCommitMessage
        {
            PreCommit = preCommit3
        };
        transportA.Post(transportB.Peer, preCommitMessage3);

        await preCommitTimeoutTask.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(1, consensusService.Height);
        Assert.Equal(1, consensusService.Round);
    }
}
