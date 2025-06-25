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
    public class ContextProposerValidRoundTest
    {
        private const int Timeout = 30000;
        private readonly ILogger _logger;

        public ContextProposerValidRoundTest(ITestOutputHelper output)
        {
            const string outputTemplate =
                "{Timestamp:HH:mm:ss:ffffffZ} - {Message} {Exception}";
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(output, outputTemplate: outputTemplate)
                .CreateLogger()
                .ForContext<ContextProposerValidRoundTest>();

            _logger = Log.ForContext<ContextProposerValidRoundTest>();
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterValidRoundPreVoteBlock()
        {
            ConsensusProposalMessage? proposal = null;
            var proposalSent = new AsyncAutoResetEvent();
            var roundTwoVoteSent = new AsyncAutoResetEvent();
            var stateChangedToRoundTwoPropose = new AsyncAutoResetEvent();
            bool timeoutProcessed = false;

            await using var consensus = TestUtils.CreateConsensus();
            // using var _1 = consensus.StateChanged.Subscribe(state =>
            // {
            //     if (state.Round == 2 && state.Step == ConsensusStep.Propose)
            //     {
            //         stateChangedToRoundTwoPropose.Set();
            //     }
            // });
            // consensus.TimeoutProcessed += (_, __) => timeoutProcessed = true;
            // using var _2 = consensus.MessagePublished.Subscribe(message =>
            // {
            //     if (message is ConsensusProposalMessage proposalMsg)
            //     {
            //         proposal = proposalMsg;
            //         proposalSent.Set();
            //     }
            //     else if (message is ConsensusPreVoteMessage prevote &&
            //         prevote.BlockHash is { } hash &&
            //         hash.Equals(proposal?.BlockHash) &&
            //         prevote.Round == 2)
            //     {
            //         roundTwoVoteSent.Set();
            //     }
            // });

            await consensus.StartAsync(default);
            await proposalSent.WaitAsync();
            Assert.NotNull(proposal);
            Block proposedBlock = proposal.Proposal.Block;

            // Force round change.
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[0],
                        TestUtils.Validators[0].Power,
                        1,
                        round: 2,
                        hash: proposedBlock.BlockHash,
                        flag: VoteType.PreVote)
                });
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        height: 1,
                        round: 2,
                        hash: proposedBlock.BlockHash,
                        flag: VoteType.PreVote)
                });
            await stateChangedToRoundTwoPropose.WaitAsync();
            Assert.Equal(2, consensus.Round);

            consensus.ProduceMessage(TestUtils.CreateConsensusPropose(
                proposedBlock, TestUtils.PrivateKeys[3], round: 2, validRound: 1));
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[0],
                        TestUtils.Validators[0].Power,
                        height: 1,
                        round: 1,
                        hash: proposedBlock.BlockHash,
                        flag: VoteType.PreVote)
                });
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        height: 1,
                        round: 1,
                        hash: proposedBlock.BlockHash,
                        flag: VoteType.PreVote)
                });
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        1,
                        round: 1,
                        hash: proposedBlock.BlockHash,
                        flag: VoteType.PreVote)
                });

            await roundTwoVoteSent.WaitAsync();
            Assert.False(timeoutProcessed); // Assert no transition is due to timeout.
            Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        }

        [Fact(Timeout = Timeout)]
        public async Task EnterValidRoundPreVoteNil()
        {
            ConsensusProposalMessage? proposal = null;
            var proposalSent = new AsyncAutoResetEvent();
            var stateChangedToRoundTwoPropose = new AsyncAutoResetEvent();
            var stateChangedToRoundTwoPreVote = new AsyncAutoResetEvent();
            var stateChangedToRoundTwoPreCommit = new AsyncAutoResetEvent();
            var stateChangedToRoundThreePropose = new AsyncAutoResetEvent();
            var roundThreeNilPreVoteSent = new AsyncAutoResetEvent();
            bool timeoutProcessed = false;
            var blockchain = TestUtils.CreateBlockChain();
            await using var consensus = TestUtils.CreateConsensus(blockchain);
            // using var _0 = consensus.StateChanged.Subscribe(state =>
            // {
            //     if (state.Round == 2 && state.Step == ConsensusStep.Propose)
            //     {
            //         stateChangedToRoundTwoPropose.Set();
            //     }
            //     else if (state.Round == 2 && state.Step == ConsensusStep.PreVote)
            //     {
            //         stateChangedToRoundTwoPreVote.Set();
            //     }
            //     else if (state.Round == 2 && state.Step == ConsensusStep.PreCommit)
            //     {
            //         stateChangedToRoundTwoPreCommit.Set();
            //     }
            //     else if (state.Round == 3 && state.Step == ConsensusStep.Propose)
            //     {
            //         stateChangedToRoundThreePropose.Set();
            //     }
            // });
            // consensus.TimeoutProcessed += (_, __) => timeoutProcessed = true;
            // using var _1 = consensus.MessagePublished.Subscribe(message =>
            // {
            //     if (message is ConsensusProposalMessage proposalMsg)
            //     {
            //         proposal = proposalMsg;
            //         proposalSent.Set();
            //     }
            //     else if (message is ConsensusPreVoteMessage prevote &&
            //         prevote.Round == 3 &&
            //         prevote.BlockHash.Equals(default))
            //     {
            //         roundThreeNilPreVoteSent.Set();
            //     }
            // });

            var key = new PrivateKey();
            var differentBlock = new RawBlock
            {
                Header = new BlockHeader
                {
                    Version = BlockHeader.CurrentProtocolVersion,
                    Height = blockchain.Tip.Height + 1,
                    Timestamp = blockchain.Tip.Timestamp.Add(TimeSpan.FromSeconds(1)),
                    Proposer = key.Address,
                    PreviousHash = blockchain.Tip.BlockHash,
                },
            }.Sign(key);

            await consensus.StartAsync(default);
            await proposalSent.WaitAsync();
            Assert.NotNull(proposal);
            Block proposedBlock = proposal.Proposal.Block;

            // Force round change to 2.
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[0],
                        TestUtils.Validators[0].Power,
                        height: 1,
                        round: 2,
                        hash: proposedBlock.BlockHash,
                        flag: VoteType.PreVote)
                });
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        height: 1,
                        round: 2,
                        hash: proposedBlock.BlockHash,
                        flag: VoteType.PreVote)
                });
            await stateChangedToRoundTwoPropose.WaitAsync();
            Assert.Equal(2, consensus.Round);
            Assert.False(timeoutProcessed); // Assert no transition is due to timeout.

            // Updated locked round and valid round to 2.
            consensus.ProduceMessage(
                TestUtils.CreateConsensusPropose(
                    proposedBlock,
                    TestUtils.PrivateKeys[3],
                    height: 1,
                    round: 2,
                    validRound: -1));
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        height: 1,
                        round: 2,
                        hash: proposedBlock.BlockHash,
                        flag: VoteType.PreVote)
                });
            await stateChangedToRoundTwoPreCommit.WaitAsync();

            // Force round change to 3.
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[0],
                        TestUtils.Validators[0].Power,
                        height: 1,
                        round: 3,
                        hash: differentBlock.BlockHash,
                        flag: VoteType.PreVote)
                });
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[2],
                        TestUtils.Validators[2].Power,
                        height: 1,
                        round: 3,
                        hash: differentBlock.BlockHash,
                        flag: VoteType.PreVote)
                });
            await stateChangedToRoundThreePropose.WaitAsync();
            Assert.Equal(3, consensus.Round);

            consensus.ProduceMessage(TestUtils.CreateConsensusPropose(
                differentBlock, TestUtils.PrivateKeys[0], round: 3, validRound: 0));
            consensus.ProduceMessage(
                new ConsensusPreVoteMessage
                {
                    PreVote = TestUtils.CreateVote(
                        TestUtils.PrivateKeys[3],
                        TestUtils.Validators[3].Power,
                        height: 1,
                        round: 3,
                        hash: differentBlock.BlockHash,
                        flag: VoteType.PreVote)
                });

            await roundThreeNilPreVoteSent.WaitAsync();
            Assert.False(timeoutProcessed);
        }
    }
}
