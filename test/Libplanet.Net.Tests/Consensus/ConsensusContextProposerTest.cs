using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;
using Nito.AsyncEx;
using Serilog;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Consensus
{
    public class ConsensusContextProposerTest
    {
        private const int Timeout = 30000;
        private readonly ILogger _logger;

        public ConsensusContextProposerTest(ITestOutputHelper output)
        {
            const string outputTemplate =
                "{Timestamp:HH:mm:ss:ffffffZ} - {Message} {Exception}";
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(output, outputTemplate: outputTemplate)
                .CreateLogger()
                .ForContext<ConsensusContextProposerTest>();

            _logger = Log.ForContext<ConsensusContextProposerTest>();
        }

        [Fact(Timeout = Timeout)]
        public async Task IncreaseRoundWhenTimeout()
        {
            var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
                TimeSpan.FromSeconds(1),
                TestUtils.Options,
                TestUtils.PrivateKeys[1]);
            var timeoutProcessed = new AsyncAutoResetEvent();
            consensusContext.TimeoutProcessed += (_, eventArgs) =>
            {
                if (eventArgs.Height == 1)
                {
                    timeoutProcessed.Set();
                }
            };

            await consensusContext.StartAsync(default);

            // Wait for block to be proposed.
            Assert.Equal(1, consensusContext.Height);
            Assert.Equal(0, consensusContext.Round);

            // Triggers timeout +2/3 with NIL and Block
            consensusContext.HandleMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteType.PreVote)
                });

            consensusContext.HandleMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteType.PreVote)
                });

            await timeoutProcessed.WaitAsync();

            consensusContext.HandleMessage(
                new ConsensusPreCommitMessage
                {
                    PreCommit = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteType.PreCommit)
                });

            consensusContext.HandleMessage(
                new ConsensusPreCommitMessage
                {
                    PreCommit = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteType.PreCommit)
                });

            await timeoutProcessed.WaitAsync();
            Assert.Equal(1, consensusContext.Height);
            Assert.Equal(1, consensusContext.Round);
        }
    }
}
