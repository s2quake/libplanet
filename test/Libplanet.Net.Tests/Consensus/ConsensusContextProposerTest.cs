using Libplanet.Net.Messages;
using Libplanet.Types;
using Xunit.Abstractions;
using Libplanet.TestUtilities.Extensions;

namespace Libplanet.Net.Tests.Consensus;

public class ConsensusContextProposerTest(ITestOutputHelper output)
{
    private const int Timeout = 30000;

    [Fact(Timeout = Timeout)]
    public async Task IncreaseRoundWhenTimeout()
    {
        var blockchain = TestUtils.MakeBlockchain();
        await using var transportA = TestUtils.CreateTransport();
        await using var transportB = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transportB,
            blockchain: blockchain,
            newHeightDelay: TimeSpan.FromSeconds(1),
            key: TestUtils.PrivateKeys[1]);
        var timeoutProcessed = new AutoResetEvent(false);
        using var _1 = consensusService.TimeoutOccurred.Subscribe(e =>
        {
            if (consensusService.Height == 1)
            {
                timeoutProcessed.Set();
            }
        });
        // consensusService.TimeoutProcessed += (_, eventArgs) =>
        // {
        //     if (eventArgs.Height == 1)
        //     {
        //         timeoutProcessed.Set();
        //     }
        // };

        await consensusService.StartAsync(default);

        // Wait for block to be proposed.
        Assert.Equal(1, consensusService.Height);
        Assert.Equal(0, consensusService.Round);

        // Triggers timeout +2/3 with NIL and Block
        transportA.Post(
            transportB.Peer,
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteMetadata
                {
                    Validator = TestUtils.Validators[2].Address,
                    ValidatorPower = TestUtils.Validators[2].Power,
                    Height = 1,
                    Type = VoteType.PreVote,
                }.Sign(TestUtils.PrivateKeys[2])
            });

        transportA.Post(
            transportB.Peer,
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteMetadata
                {
                    Validator = TestUtils.Validators[3].Address,
                    ValidatorPower = TestUtils.Validators[3].Power,
                    Height = 1,
                    Type = VoteType.PreVote,
                }.Sign(TestUtils.PrivateKeys[3])
            });

        Assert.True(timeoutProcessed.WaitOne(10000), "Timeout did not occur as expected.");

        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[2].Address,
                    ValidatorPower = TestUtils.Validators[2].Power,
                    Height = 1,
                    Type = VoteType.PreCommit,
                }.Sign(TestUtils.PrivateKeys[2])
            });

        transportA.Post(
            transportB.Peer,
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteMetadata
                {
                    Validator = TestUtils.Validators[3].Address,
                    ValidatorPower = TestUtils.Validators[3].Power,
                    Height = 1,
                    Type = VoteType.PreCommit,
                }.Sign(TestUtils.PrivateKeys[3])
            });

        Assert.True(timeoutProcessed.WaitOne(10000), "Timeout did not occur as expected.");
        Assert.Equal(1, consensusService.Height);
        Assert.Equal(1, consensusService.Round);
    }
}
