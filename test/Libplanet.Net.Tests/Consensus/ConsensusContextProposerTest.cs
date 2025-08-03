using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;
using Nito.AsyncEx;
using Serilog;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Consensus;

public class ConsensusContextProposerTest(ITestOutputHelper output)
{
    private const int Timeout = 30000;

    [Fact(Timeout = Timeout)]
    public async Task IncreaseRoundWhenTimeout()
    {
        var blockchain = Libplanet.Tests.TestUtils.MakeBlockchain();
        var consensusService = TestUtils.CreateConsensusService(
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
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[2],
                    TestUtils.Validators[2].Power,
                    1,
                    0,
                    hash: default,
                    flag: VoteType.PreVote)
            },
            default);

        await consensusService.HandleMessageAsync(
            new ConsensusPreVoteMessage
            {
                PreVote = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[3],
                    TestUtils.Validators[3].Power,
                    1,
                    0,
                    hash: default,
                    flag: VoteType.PreVote)
            },
            default);

        Assert.True(timeoutProcessed.WaitOne(10000), "Timeout did not occur as expected.");

        await consensusService.HandleMessageAsync(
            new ConsensusPreCommitMessage
            {
                PreCommit = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[2],
                    TestUtils.Validators[2].Power,
                    1,
                    0,
                    hash: default,
                    flag: VoteType.PreCommit)
            },
            default);

        await consensusService.HandleMessageAsync(
            new ConsensusPreCommitMessage
            {
                PreCommit = TestUtils.CreateVote(
                    TestUtils.PrivateKeys[3],
                    TestUtils.Validators[3].Power,
                    1,
                    0,
                    hash: default,
                    flag: VoteType.PreCommit)
            },
            default);

        Assert.True(timeoutProcessed.WaitOne(10000), "Timeout did not occur as expected.");
        Assert.Equal(1, consensusService.Height);
        Assert.Equal(1, consensusService.Round);
    }
}
