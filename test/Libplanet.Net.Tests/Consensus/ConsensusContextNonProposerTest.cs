using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.TestUtilities.Extensions;
using Nito.AsyncEx;
using Serilog;
using xRetry;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Consensus
{
    public class ConsensusContextNonProposerTest
    {
        private const int Timeout = 30000;
        private readonly ILogger _logger;

        public ConsensusContextNonProposerTest(ITestOutputHelper output)
        {
            const string outputTemplate =
                "{Timestamp:HH:mm:ss:ffffffZ} - {Message} {Exception}";
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(output, outputTemplate: outputTemplate)
                .CreateLogger()
                .ForContext<ConsensusContextNonProposerTest>();

            _logger = Log.ForContext<ConsensusContextNonProposerTest>();
        }

        [Fact(Timeout = Timeout)]
        public async Task NewHeightWithLastCommit()
        {
            var tipChanged = new AsyncAutoResetEvent();
            ConsensusProposalMessage? proposal = null;
            var heightTwoProposalSent = new AsyncAutoResetEvent();
            var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
                TimeSpan.FromSeconds(1),
                TestUtils.Options,
                TestUtils.PrivateKeys[2]);
            using var _ = blockChain.TipChanged.Subscribe(e => tipChanged.Set());
            // consensusContext.MessagePublished += (_, eventArgs) =>
            // {
            //     if (eventArgs.Height == 2 && eventArgs.Message is ConsensusProposalMessage proposalMsg)
            //     {
            //         proposal = proposalMsg;
            //         heightTwoProposalSent.Set();
            //     }
            // };

            await consensusContext.StartAsync(default);
            var block1 = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            consensusContext.HandleMessage(
                TestUtils.CreateConsensusPropose(block1, TestUtils.PrivateKeys[1]));
            var expectedVotes = new Vote[4];

            // Peer2 sends a ConsensusVote via background process.
            // Enough votes are present to proceed even without Peer3's vote.
            for (int i = 0; i < 2; i++)
            {
                expectedVotes[i] = new VoteMetadata
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = block1.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = TestUtils.Validators[i].Address,
                    ValidatorPower = TestUtils.Validators[i].Power,
                    Type = VoteType.PreVote,
                }.Sign(TestUtils.PrivateKeys[i]);
                consensusContext.HandleMessage(new ConsensusPreVoteMessage { PreVote = expectedVotes[i] });
            }

            // Peer2 sends a ConsensusCommit via background process.
            // Enough votes are present to proceed even without Peer3's vote.
            for (int i = 0; i < 2; i++)
            {
                expectedVotes[i] = new VoteMetadata
                {
                    Height = 1,
                    Round = 0,
                    BlockHash = block1.BlockHash,
                    Timestamp = DateTimeOffset.UtcNow,
                    Validator = TestUtils.Validators[i].Address,
                    ValidatorPower = TestUtils.Validators[i].Power,
                    Type = VoteType.PreCommit,
                }.Sign(TestUtils.PrivateKeys[i]);
                consensusContext.HandleMessage(new ConsensusPreCommitMessage { PreCommit = expectedVotes[i] });
            }

            await heightTwoProposalSent.WaitAsync();
            Assert.NotNull(proposal);

            Block proposedBlock = proposal!.Proposal.Block;
            ImmutableArray<Vote> votes = proposedBlock.PreviousCommit.Votes is { } vs
                ? vs
                : throw new NullReferenceException();
            Assert.Equal(VoteType.PreCommit, votes[0].Type);
            Assert.Equal(VoteType.PreCommit, votes[1].Type);
            Assert.Equal(VoteType.PreCommit, votes[2].Type);
            Assert.Equal(VoteType.Null, votes[3].Type);
        }

        [Fact(Timeout = Timeout)]
        public async Task HandleMessageFromHigherHeight()
        {
            ConsensusProposalMessage? proposal = null;
            var heightTwoStepChangedToPreVote = new AsyncAutoResetEvent();
            var heightTwoStepChangedToPreCommit = new AsyncAutoResetEvent();
            var heightTwoStepChangedToEndCommit = new AsyncAutoResetEvent();
            var heightThreeStepChangedToPropose = new AsyncAutoResetEvent();
            var heightThreeStepChangedToPreVote = new AsyncAutoResetEvent();
            var proposalSent = new AsyncAutoResetEvent();
            var newHeightDelay = TimeSpan.FromSeconds(1);

            var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
                newHeightDelay,
                TestUtils.Options,
                TestUtils.PrivateKeys[2]);
            await consensusContext.StartAsync(default);

            // consensusContext.StateChanged += (_, eventArgs) =>
            // {
            //     if (eventArgs.Height == 2)
            //     {
            //         if (eventArgs.Step == ConsensusStep.PreVote)
            //         {
            //             heightTwoStepChangedToPreVote.Set();
            //         }
            //         else if (eventArgs.Step == ConsensusStep.PreCommit)
            //         {
            //             heightTwoStepChangedToPreCommit.Set();
            //         }
            //         else if (eventArgs.Step == ConsensusStep.EndCommit)
            //         {
            //             heightTwoStepChangedToEndCommit.Set();
            //         }
            //     }
            //     else if (eventArgs.Height == 3)
            //     {
            //         if (eventArgs.Step == ConsensusStep.Propose)
            //         {
            //             heightThreeStepChangedToPropose.Set();
            //         }
            //         else if (eventArgs.Step == ConsensusStep.PreVote)
            //         {
            //             heightThreeStepChangedToPreVote.Set();
            //         }
            //     }
            // };
            // consensusContext.MessagePublished += (_, eventArgs) =>
            // {
            //     if (eventArgs.Message is ConsensusProposalMessage proposalMsg)
            //     {
            //         proposal = proposalMsg;
            //         proposalSent.Set();
            //     }
            // };

            Block block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            blockChain.Append(block, TestUtils.CreateBlockCommit(block));

            // blockChain._repository.BlockCommits.Add(TestUtils.CreateBlockCommit(blockChain.Blocks[1]));
            await proposalSent.WaitAsync();

            Assert.Equal(2, consensusContext.Height);

            if (proposal is null)
            {
                throw new Exception("Proposal is null.");
            }

            foreach ((PrivateKey privateKey, BigInteger power)
                     in TestUtils.PrivateKeys.Zip(
                         TestUtils.Validators.Select(v => v.Power),
                         (first, second) => (first, second)))
            {
                if (privateKey == TestUtils.PrivateKeys[2])
                {
                    // Peer2 will send a ConsensusVote by handling the ConsensusPropose message.
                    continue;
                }

                consensusContext.HandleMessage(
                    new ConsensusPreVoteMessage
                    {
                        PreVote = new VoteMetadata
                        {
                            Height = 2,
                            Round = 0,
                            BlockHash = proposal!.BlockHash,
                            Timestamp = DateTimeOffset.UtcNow,
                            Validator = privateKey.Address,
                            ValidatorPower = power,
                            Type = VoteType.PreVote,
                        }.Sign(privateKey)
                    });
            }

            foreach ((PrivateKey privateKey, BigInteger power)
                     in TestUtils.PrivateKeys.Zip(
                         TestUtils.Validators.Select(v => v.Power),
                         (first, second) => (first, second)))
            {
                if (privateKey == TestUtils.PrivateKeys[2])
                {
                    // Peer2 will send a ConsensusCommit by handling the ConsensusVote message.
                    continue;
                }

                consensusContext.HandleMessage(
                    new ConsensusPreCommitMessage
                    {
                        PreCommit = new VoteMetadata
                        {
                            Height = 2,
                            Round = 0,
                            BlockHash = proposal!.BlockHash,
                            Timestamp = DateTimeOffset.UtcNow,
                            Validator = privateKey.Address,
                            ValidatorPower = power,
                            Type = VoteType.PreCommit,
                        }.Sign(privateKey)
                    });
            }

            await heightTwoStepChangedToEndCommit.WaitAsync();

            var blockHeightTwo = proposal.Proposal.Block;
            var blockHeightThree = blockChain.ProposeBlock(TestUtils.PrivateKeys[3]);

            // Message from higher height
            consensusContext.HandleMessage(
                TestUtils.CreateConsensusPropose(blockHeightThree, TestUtils.PrivateKeys[3], 3));

            // New height started.
            await heightThreeStepChangedToPropose.WaitAsync();
            // Propose -> PreVote (message consumed)
            await heightThreeStepChangedToPreVote.WaitAsync();
            Assert.Equal(3, consensusContext.Height);
            Assert.Equal(ConsensusStep.PreVote, consensusContext.Step);
        }

        [Fact(Timeout = Timeout)]
        public async Task UseLastCommitCacheIfHeightContextIsEmpty()
        {
            var heightTwoProposalSent = new AsyncAutoResetEvent();
            Block? proposedBlock = null;

            var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
                TimeSpan.FromSeconds(1),
                TestUtils.Options,
                TestUtils.PrivateKeys[2]);
            // consensusContext.MessageConsumed += (_, eventArgs) =>
            // {
            //     if (eventArgs.Height == 2 &&
            //         eventArgs.Message is ConsensusProposalMessage propose)
            //     {
            //         proposedBlock = propose!.Proposal.Block;
            //         heightTwoProposalSent.Set();
            //     }
            // };

            await consensusContext.StartAsync(default);
            Block block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            var createdLastCommit = TestUtils.CreateBlockCommit(block);
            blockChain.Append(block, createdLastCommit);

            // Context for height #2 where node #2 is the proposer is automatically started
            // by watching blockchain's Tip.
            await heightTwoProposalSent.WaitAsync();
            Assert.NotNull(proposedBlock);
            Assert.Equal(2, proposedBlock!.Height);
            Assert.Equal(createdLastCommit, proposedBlock!.PreviousCommit);
        }

        // Retry: This calculates delta time.
        [RetryFact(10, Timeout = Timeout)]
        public async Task NewHeightDelay()
        {
            var newHeightDelay = TimeSpan.FromSeconds(1);
            // The maximum error margin. (macos-netcore-test)
            var timeError = 500;
            var heightOneEndCommit = new AsyncAutoResetEvent();
            var heightTwoProposalSent = new AsyncAutoResetEvent();
            var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
                newHeightDelay,
                TestUtils.Options,
                TestUtils.PrivateKeys[2]);
            // consensusContext.StateChanged += (_, eventArgs) =>
            // {
            //     if (eventArgs.Height == 1 && eventArgs.Step == ConsensusStep.EndCommit)
            //     {
            //         heightOneEndCommit.Set();
            //     }
            // };
            // consensusContext.MessagePublished += (_, eventArgs) =>
            // {
            //     if (eventArgs.Height == 2 && eventArgs.Message is ConsensusProposalMessage)
            //     {
            //         heightTwoProposalSent.Set();
            //     }
            // };

            await consensusContext.StartAsync(default);
            var block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            consensusContext.HandleMessage(
                TestUtils.CreateConsensusPropose(block, TestUtils.PrivateKeys[1]));

            TestUtils.HandleFourPeersPreCommitMessages(
                 consensusContext, TestUtils.PrivateKeys[2], block.BlockHash);

            await heightOneEndCommit.WaitAsync();
            var endCommitTime = DateTimeOffset.UtcNow;

            await heightTwoProposalSent.WaitAsync();
            var proposeTime = DateTimeOffset.UtcNow;
            var difference = proposeTime - endCommitTime;

            _logger.Debug("Difference: {Difference}", difference);
            // Check new height delay; slight margin of error is allowed as delay task
            // is run asynchronously from context events.
            Assert.True(
                ((proposeTime - endCommitTime) - newHeightDelay).Duration() <
                    TimeSpan.FromMilliseconds(timeError));
        }
    }
}
