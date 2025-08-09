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
        await using var transport = TestUtils.CreateTransport();
        await using var consensusService = TestUtils.CreateConsensusService(
            transport,
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
        await consensusService.HandleMessageAsync(
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteBuilder
                {
                    Validator = TestUtils.Validators[2],
                    Height = 1,
                    Type = VoteType.PreVote,
                }.Create(TestUtils.PrivateKeys[2])
            },
            default);

        await consensusService.HandleMessageAsync(
            new ConsensusPreVoteMessage
            {
                PreVote = new VoteBuilder
                {
                    Validator = TestUtils.Validators[3],
                    Height = 1,
                    Type = VoteType.PreVote,
                }.Create(TestUtils.PrivateKeys[3])
            },
            default);

        Assert.True(timeoutProcessed.WaitOne(10000), "Timeout did not occur as expected.");

        await consensusService.HandleMessageAsync(
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteBuilder
                {
                    Validator = TestUtils.Validators[2],
                    Height = 1,
                    Type = VoteType.PreCommit,
                }.Create(TestUtils.PrivateKeys[2])
            },
            default);

        await consensusService.HandleMessageAsync(
            new ConsensusPreCommitMessage
            {
                PreCommit = new VoteBuilder
                {
                    Validator = TestUtils.Validators[3],
                    Height = 1,
                    Type = VoteType.PreCommit,
                }.Create(TestUtils.PrivateKeys[3])
            },
            default);

        Assert.True(timeoutProcessed.WaitOne(10000), "Timeout did not occur as expected.");
        Assert.Equal(1, consensusService.Height);
        Assert.Equal(1, consensusService.Round);
    }
}
