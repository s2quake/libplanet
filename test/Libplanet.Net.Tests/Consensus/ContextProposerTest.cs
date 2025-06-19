using System.Threading.Tasks;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Serialization;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Nito.AsyncEx;
using Serilog;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Consensus
{
    public class ContextProposerTest
    {
        private const int Timeout = 30000;
        private readonly ILogger _logger;

        public ContextProposerTest(ITestOutputHelper output)
        {
            const string outputTemplate =
                "{Timestamp:HH:mm:ss:ffffffZ} - {Message} {Exception}";
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(output, outputTemplate: outputTemplate)
                .CreateLogger()
                .ForContext<ContextProposerTest>();

            _logger = Log.ForContext<ContextProposerTest>();
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterPreCommitNil()
        {
            var stepChangedToPreCommit = new AsyncAutoResetEvent();
            ConsensusPreCommitMessage? preCommit = null;
            var preCommitSent = new AsyncAutoResetEvent();

            var (_, context) = TestUtils.CreateDummyContext();
            using var _1 = context.StateChanged.Subscribe(state =>
            {
                if (state.Step == ConsensusStep.PreCommit)
                {
                    stepChangedToPreCommit.Set();
                }
            });
            using var _2 = context.MessagePublished.Subscribe(message =>
            {
                if (message is ConsensusPreCommitMessage preCommitMsg)
                {
                    preCommit = preCommitMsg;
                    preCommitSent.Set();
                }
            });

            await context.StartAsync(default);
            context.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[0],
                        TestUtils.Validators[0].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteFlag.PreVote)
                });
            context.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteFlag.PreVote)
                });
            context.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteFlag.PreVote)
                });

            await Task.WhenAll(preCommitSent.WaitAsync(), stepChangedToPreCommit.WaitAsync());
            Assert.Equal(default(BlockHash), preCommit?.BlockHash);
            Assert.Equal(1, context.Height);
            Assert.Equal(0, context.Round);
            Assert.Equal(ConsensusStep.PreCommit, context.Step);
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterPreCommitBlock()
        {
            var stepChangedToPreCommit = new AsyncAutoResetEvent();
            ConsensusProposalMessage? proposal = null;
            var proposalSent = new AsyncAutoResetEvent();
            ConsensusPreCommitMessage? preCommit = null;
            var preCommitSent = new AsyncAutoResetEvent();

            var (_, context) = TestUtils.CreateDummyContext();
            using var _1 = context.StateChanged.Subscribe(state =>
            {
                if (state.Step == ConsensusStep.PreCommit)
                {
                    stepChangedToPreCommit.Set();
                }
            });
            using var _2 = context.MessagePublished.Subscribe(message =>
            {
                if (message is ConsensusProposalMessage proposalMsg)
                {
                    proposal = proposalMsg;
                    proposalSent.Set();
                }
                else if (message is ConsensusPreCommitMessage preCommitMsg)
                {
                    preCommit = preCommitMsg;
                    preCommitSent.Set();
                }
            });

            await context.StartAsync(default);

            // Wait for propose to process.
            await proposalSent.WaitAsync();
            BlockHash proposedblockHash = Assert.IsType<BlockHash>(proposal?.BlockHash);

            context.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[0],
                        TestUtils.Validators[0].Power,
                        1,
                        0,
                        hash: proposedblockHash,
                        flag: VoteFlag.PreVote)
                });
            context.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        0,
                        hash: proposedblockHash,
                        flag: VoteFlag.PreVote)
                });
            context.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        0,
                        hash: proposedblockHash,
                        flag: VoteFlag.PreVote)
                });

            await Task.WhenAll(preCommitSent.WaitAsync(), stepChangedToPreCommit.WaitAsync());
            Assert.Equal(proposedblockHash, preCommit?.BlockHash);
            Assert.Equal(1, context.Height);
            Assert.Equal(0, context.Round);
            Assert.Equal(ConsensusStep.PreCommit, context.Step);
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterNewRoundNil()
        {
            var roundChangedToOne = new AsyncAutoResetEvent();

            var (_, context) = TestUtils.CreateDummyContext();
            using var _ = context.StateChanged.Subscribe(state =>
            {
                if (state.Round == 1)
                {
                    roundChangedToOne.Set();
                }
            });

            await context.StartAsync(default);
            context.ProduceMessage(
                new ConsensusPreCommitMessage
                {
                    PreCommit = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[0],
                        TestUtils.Validators[0].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteFlag.PreCommit)
                });
            context.ProduceMessage(
                new ConsensusPreCommitMessage
                {
                    PreCommit = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteFlag.PreCommit)
                });
            context.ProduceMessage(
                new ConsensusPreCommitMessage
                {
                    PreCommit = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteFlag.PreCommit)
                });

            await roundChangedToOne.WaitAsync();
            Assert.Equal(1, context.Height);
            Assert.Equal(1, context.Round);
            Assert.Equal(ConsensusStep.Propose, context.Step);
        }

        [Fact(Timeout = Timeout)]
        public async Task EndCommitBlock()
        {
            var stepChangedToEndCommit = new AsyncAutoResetEvent();
            var stepChangedToPreCommit = new AsyncAutoResetEvent();
            ConsensusProposalMessage? proposal = null;
            var proposalSent = new AsyncAutoResetEvent();

            var (_, context) = TestUtils.CreateDummyContext();
            using var _1 = context.StateChanged.Subscribe(state =>
            {
                if (state.Step == ConsensusStep.PreCommit)
                {
                    stepChangedToPreCommit.Set();
                }

                if (state.Step == ConsensusStep.EndCommit)
                {
                    stepChangedToEndCommit.Set();
                }
            });
            using var _2 = context.MessagePublished.Subscribe(message =>
            {
                if (message is ConsensusProposalMessage proposalMsg)
                {
                    proposal = proposalMsg;
                    proposalSent.Set();
                }
            });

            await context.StartAsync(default);

            // Wait for propose to process.
            await proposalSent.WaitAsync();
            Assert.NotNull(proposal?.BlockHash);

            TestUtils.HandleFourPeersPreVoteMessages(
                context,
                TestUtils.PrivateKeys[1],
                proposal!.Proposal.BlockHash);

            await stepChangedToPreCommit.WaitAsync();

            TestUtils.HandleFourPeersPreCommitMessages(
                context,
                TestUtils.PrivateKeys[1],
                proposal!.Proposal.BlockHash);

            await stepChangedToEndCommit.WaitAsync();
            Assert.Equal(proposal?.BlockHash, context.GetBlockCommit().BlockHash);
            Assert.Equal(1, context.Height);
            Assert.Equal(0, context.Round);
            Assert.Equal(ConsensusStep.EndCommit, context.Step);
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterPreVoteNil()
        {
            var stepChangedToPreVote = new AsyncAutoResetEvent();
            var nilPreVoteSent = new AsyncAutoResetEvent();
            var (_, context) = TestUtils.CreateDummyContext(
                height: 5,
                validatorSet: Libplanet.Tests.TestUtils.Validators); // Peer1 should be a proposer

            using var _1 = context.StateChanged.Subscribe(state =>
            {
                if (state.Step == ConsensusStep.PreVote)
                {
                    stepChangedToPreVote.Set();
                }
            });
            using var _2 = context.MessagePublished.Subscribe(message =>
            {
                if (message is ConsensusPreVoteMessage vote && vote.PreVote.BlockHash.Equals(default))
                {
                    nilPreVoteSent.Set();
                }
            });

            await context.StartAsync(default);
            await Task.WhenAll(nilPreVoteSent.WaitAsync(), stepChangedToPreVote.WaitAsync());
            Assert.Equal(ConsensusStep.PreVote, context.Step);
            Assert.Equal(5, context.Height);
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterPreVoteBlock()
        {
            var stepChangedToPreVote = new AsyncAutoResetEvent();
            ConsensusProposalMessage? proposal = null;
            var proposalSent = new AsyncAutoResetEvent();
            ConsensusPreVoteMessage? preVote = null;
            var preVoteSent = new AsyncAutoResetEvent();

            var (_, context) = TestUtils.CreateDummyContext();

            using var _1 = context.StateChanged.Subscribe(state =>
            {
                if (state.Step == ConsensusStep.PreVote)
                {
                    stepChangedToPreVote.Set();
                }
            });
            using var _2 = context.MessagePublished.Subscribe(message =>
            {
                if (message is ConsensusProposalMessage proposalMsg)
                {
                    proposal = proposalMsg;
                    proposalSent.Set();
                }
                else if (message is ConsensusPreVoteMessage preVoteMsg)
                {
                    preVote = preVoteMsg;
                    preVoteSent.Set();
                }
            });

            await context.StartAsync(default);
            await proposalSent.WaitAsync();
            Assert.NotNull(proposal?.BlockHash);

            await Task.WhenAll(preVoteSent.WaitAsync(), stepChangedToPreVote.WaitAsync());
            Assert.Equal(proposal?.BlockHash, preVote?.BlockHash);
            Assert.Equal(1, context.Height);
            Assert.Equal(0, context.Round);
            Assert.Equal(ConsensusStep.PreVote, context.Step);
        }

        [Fact(Timeout = Timeout)]
        public async Task VoteNilOnSelfProposedInvalidBlock()
        {
            var privateKey = new PrivateKey();
            ConsensusProposalMessage? proposal = null;
            var proposalSent = new AsyncAutoResetEvent();
            ConsensusPreVoteMessage? preVote = null;
            var preVoteSent = new AsyncAutoResetEvent();

            var blockChain = TestUtils.CreateDummyBlockChain();
            var block1 = blockChain.ProposeBlock(new PrivateKey());
            var block1Commit = TestUtils.CreateBlockCommit(block1);
            blockChain.Append(block1, block1Commit);
            var block2 = blockChain.ProposeBlock(new PrivateKey());
            var block2Commit = TestUtils.CreateBlockCommit(block2);
            blockChain.Append(block2, block2Commit);

            var context = TestUtils.CreateDummyContext(
                blockChain,
                privateKey: TestUtils.PrivateKeys[2],
                height: 2,
                lastCommit: block2Commit,
                validatorSet: TestUtils.Validators);
            using var _ = context.MessagePublished.Subscribe(message =>
            {
                if (message is ConsensusProposalMessage proposalMsg)
                {
                    proposal = proposalMsg;
                    proposalSent.Set();
                }
                else if (message is ConsensusPreVoteMessage preVoteMsg)
                {
                    preVote = preVoteMsg;
                    preVoteSent.Set();
                }
            });

            Assert.Equal(
                TestUtils.PrivateKeys[2].Address,
                TestUtils.Validators.GetProposer(2, 0).Address);

            await context.StartAsync(default);
            await proposalSent.WaitAsync();
            var proposedBlock = ModelSerializer.DeserializeFromBytes<Block>(
                proposal?.Proposal.MarshaledBlock!);
            Assert.Equal(context.Height + 1, proposedBlock.Height);
            await preVoteSent.WaitAsync();
            Assert.Equal(default(BlockHash), preVote?.BlockHash);
            Assert.Equal(default(BlockHash), preVote?.PreVote.BlockHash);
        }
    }
}
