using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
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

            await using var consensus = TestUtils.CreateConsensus();
            // using var _1 = consensus.StateChanged.Subscribe(state =>
            // {
            //     if (state.Step == ConsensusStep.PreCommit)
            //     {
            //         stepChangedToPreCommit.Set();
            //     }
            // });
            // using var _2 = consensus.MessagePublished.Subscribe(message =>
            // {
            //     if (message is ConsensusPreCommitMessage preCommitMsg)
            //     {
            //         preCommit = preCommitMsg;
            //         preCommitSent.Set();
            //     }
            // });

            await consensus.StartAsync(default);
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[0],
                        TestUtils.Validators[0].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteType.PreVote)
                });
            consensus.ProduceMessage(
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
            consensus.ProduceMessage(
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

            await Task.WhenAll(preCommitSent.WaitAsync(), stepChangedToPreCommit.WaitAsync());
            Assert.Equal(default(BlockHash), preCommit?.BlockHash);
            Assert.Equal(1, consensus.Height);
            Assert.Equal(0, consensus.Round.Index);
            Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterPreCommitBlock()
        {
            var stepChangedToPreCommit = new AsyncAutoResetEvent();
            ConsensusProposalMessage? proposal = null;
            var proposalSent = new AsyncAutoResetEvent();
            ConsensusPreCommitMessage? preCommit = null;
            var preCommitSent = new AsyncAutoResetEvent();

            await using var consensus = TestUtils.CreateConsensus();
            // using var _1 = consensus.StateChanged.Subscribe(state =>
            // {
            //     if (state.Step == ConsensusStep.PreCommit)
            //     {
            //         stepChangedToPreCommit.Set();
            //     }
            // });
            // using var _2 = consensus.MessagePublished.Subscribe(message =>
            // {
            //     if (message is ConsensusProposalMessage proposalMsg)
            //     {
            //         proposal = proposalMsg;
            //         proposalSent.Set();
            //     }
            //     else if (message is ConsensusPreCommitMessage preCommitMsg)
            //     {
            //         preCommit = preCommitMsg;
            //         preCommitSent.Set();
            //     }
            // });

            await consensus.StartAsync(default);

            // Wait for propose to process.
            await proposalSent.WaitAsync();
            BlockHash proposedblockHash = Assert.IsType<BlockHash>(proposal?.BlockHash);

            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[0],
                        TestUtils.Validators[0].Power,
                        1,
                        0,
                        hash: proposedblockHash,
                        flag: VoteType.PreVote)
                });
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        1,
                        0,
                        hash: proposedblockHash,
                        flag: VoteType.PreVote)
                });
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        0,
                        hash: proposedblockHash,
                        flag: VoteType.PreVote)
                });

            await Task.WhenAll(preCommitSent.WaitAsync(), stepChangedToPreCommit.WaitAsync());
            Assert.Equal(proposedblockHash, preCommit?.BlockHash);
            Assert.Equal(1, consensus.Height);
            Assert.Equal(0, consensus.Round.Index);
            Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterNewRoundNil()
        {
            var roundChangedToOne = new AsyncAutoResetEvent();

            await using var consensus = TestUtils.CreateConsensus();
            // using var _ = consensus.StateChanged.Subscribe(state =>
            // {
            //     if (state.Round == 1)
            //     {
            //         roundChangedToOne.Set();
            //     }
            // });

            await consensus.StartAsync(default);
            consensus.ProduceMessage(
                new ConsensusPreCommitMessage
                {
                    PreCommit = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[0],
                        TestUtils.Validators[0].Power,
                        1,
                        0,
                        hash: default,
                        flag: VoteType.PreCommit)
                });
            consensus.ProduceMessage(
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
            consensus.ProduceMessage(
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

            await roundChangedToOne.WaitAsync();
            Assert.Equal(1, consensus.Height);
            Assert.Equal(1, consensus.Round.Index);
            Assert.Equal(ConsensusStep.Propose, consensus.Step);
        }

        [Fact(Timeout = Timeout)]
        public async Task EndCommitBlock()
        {
            var stepChangedToEndCommit = new AsyncAutoResetEvent();
            var stepChangedToPreCommit = new AsyncAutoResetEvent();
            ConsensusProposalMessage? proposal = null;
            var proposalSent = new AsyncAutoResetEvent();

            await using var consensus = TestUtils.CreateConsensus();
            // using var _1 = consensus.StateChanged.Subscribe(state =>
            // {
            //     if (state.Step == ConsensusStep.PreCommit)
            //     {
            //         stepChangedToPreCommit.Set();
            //     }

            //     if (state.Step == ConsensusStep.EndCommit)
            //     {
            //         stepChangedToEndCommit.Set();
            //     }
            // });
            // using var _2 = consensus.MessagePublished.Subscribe(message =>
            // {
            //     if (message is ConsensusProposalMessage proposalMsg)
            //     {
            //         proposal = proposalMsg;
            //         proposalSent.Set();
            //     }
            // });

            await consensus.StartAsync(default);

            // Wait for propose to process.
            await proposalSent.WaitAsync();
            Assert.NotNull(proposal?.BlockHash);

            TestUtils.HandleFourPeersPreVoteMessages(
                consensus,
                TestUtils.PrivateKeys[1],
                proposal!.Proposal.BlockHash);

            await stepChangedToPreCommit.WaitAsync();

            TestUtils.HandleFourPeersPreCommitMessages(
                consensus,
                TestUtils.PrivateKeys[1],
                proposal!.Proposal.BlockHash);

            await stepChangedToEndCommit.WaitAsync();
            Assert.Equal(proposal?.BlockHash, consensus.GetBlockCommit().BlockHash);
            Assert.Equal(1, consensus.Height);
            Assert.Equal(0, consensus.Round.Index);
            Assert.Equal(ConsensusStep.EndCommit, consensus.Step);
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterPreVoteNil()
        {
            var stepChangedToPreVote = new AsyncAutoResetEvent();
            var nilPreVoteSent = new AsyncAutoResetEvent();
            await using var consensus = TestUtils.CreateConsensus(height: 5); // Peer1 should be a proposer

            // using var _1 = consensus.StateChanged.Subscribe(state =>
            // {
            //     if (state.Step == ConsensusStep.PreVote)
            //     {
            //         stepChangedToPreVote.Set();
            //     }
            // });
            // using var _2 = consensus.MessagePublished.Subscribe(message =>
            // {
            //     if (message is ConsensusPreVoteMessage vote && vote.PreVote.BlockHash.Equals(default))
            //     {
            //         nilPreVoteSent.Set();
            //     }
            // });

            await consensus.StartAsync(default);
            await Task.WhenAll(nilPreVoteSent.WaitAsync(), stepChangedToPreVote.WaitAsync());
            Assert.Equal(ConsensusStep.PreVote, consensus.Step);
            Assert.Equal(5, consensus.Height);
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterPreVoteBlock()
        {
            var stepChangedToPreVote = new AsyncAutoResetEvent();
            ConsensusProposalMessage? proposal = null;
            var proposalSent = new AsyncAutoResetEvent();
            ConsensusPreVoteMessage? preVote = null;
            var preVoteSent = new AsyncAutoResetEvent();

            await using var consensus = TestUtils.CreateConsensus();

            // using var _1 = consensus.StateChanged.Subscribe(state =>
            // {
            //     if (state.Step == ConsensusStep.PreVote)
            //     {
            //         stepChangedToPreVote.Set();
            //     }
            // });
            // using var _2 = consensus.MessagePublished.Subscribe(message =>
            // {
            //     if (message is ConsensusProposalMessage proposalMsg)
            //     {
            //         proposal = proposalMsg;
            //         proposalSent.Set();
            //     }
            //     else if (message is ConsensusPreVoteMessage preVoteMsg)
            //     {
            //         preVote = preVoteMsg;
            //         preVoteSent.Set();
            //     }
            // });

            await consensus.StartAsync(default);
            await proposalSent.WaitAsync();
            Assert.NotNull(proposal?.BlockHash);

            await Task.WhenAll(preVoteSent.WaitAsync(), stepChangedToPreVote.WaitAsync());
            Assert.Equal(proposal?.BlockHash, preVote?.BlockHash);
            Assert.Equal(1, consensus.Height);
            Assert.Equal(0, consensus.Round.Index);
            Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        }

        [Fact(Timeout = Timeout)]
        public async Task VoteNilOnSelfProposedInvalidBlock()
        {
            var privateKey = new PrivateKey();
            ConsensusProposalMessage? proposal = null;
            var proposalSent = new AsyncAutoResetEvent();
            ConsensusPreVoteMessage? preVote = null;
            var preVoteSent = new AsyncAutoResetEvent();

            var blockChain = Libplanet.Tests.TestUtils.MakeBlockchain();
            var block1 = blockChain.ProposeBlock(new PrivateKey());
            var block1Commit = TestUtils.CreateBlockCommit(block1);
            blockChain.Append(block1, block1Commit);
            var block2 = blockChain.ProposeBlock(new PrivateKey());
            var block2Commit = TestUtils.CreateBlockCommit(block2);
            blockChain.Append(block2, block2Commit);

            await using var consensus = TestUtils.CreateConsensus(
                blockChain,
                privateKey: TestUtils.PrivateKeys[2],
                height: 2);
            // using var _ = consensus.MessagePublished.Subscribe(message =>
            // {
            //     if (message is ConsensusProposalMessage proposalMsg)
            //     {
            //         proposal = proposalMsg;
            //         proposalSent.Set();
            //     }
            //     else if (message is ConsensusPreVoteMessage preVoteMsg)
            //     {
            //         preVote = preVoteMsg;
            //         preVoteSent.Set();
            //     }
            // });

            Assert.Equal(
                TestUtils.PrivateKeys[2].Address,
                TestUtils.Validators.GetProposer(2, 0).Address);

            await consensus.StartAsync(default);
            await proposalSent.WaitAsync();
            var proposedBlock = proposal!.Proposal.Block;
            Assert.Equal(consensus.Height + 1, proposedBlock.Height);
            await preVoteSent.WaitAsync();
            Assert.Equal(default(BlockHash), preVote?.BlockHash);
            Assert.Equal(default(BlockHash), preVote?.PreVote.BlockHash);
        }
    }
}
