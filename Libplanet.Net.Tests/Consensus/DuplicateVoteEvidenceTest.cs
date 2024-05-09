using System;
using System.Numerics;
using Bencodex;
using Bencodex.Types;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Nito.AsyncEx;
using Serilog;
using Xunit;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Consensus
{
    public class DuplicatedVoteEvidenceTest
    {
        private const int Timeout = 30000;
        private static readonly Codec _codec = new Codec();
        private readonly ILogger _logger;

        public DuplicatedVoteEvidenceTest(ITestOutputHelper output)
        {
            const string outputTemplate =
                "{Timestamp:HH:mm:ss:ffffffZ} - {Message} {Exception}";
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(output, outputTemplate: outputTemplate)
                .CreateLogger()
                .ForContext<ConsensusContextTest>();

            _logger = Log.ForContext<ConsensusContextTest>();
        }

        [Fact(Timeout = Timeout)]
        public async void Evidences_WithDuplicateVotes_Test()
        {
            ConsensusProposalMsg? proposal = null;
            var proposalMessageHeightThreeSent = new AsyncAutoResetEvent();
            var proposalMessageHeightSevenSent = new AsyncAutoResetEvent();
            var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
                TimeSpan.FromSeconds(1),
                TestUtils.Policy,
                null,
                TestUtils.PrivateKeys[3]);

            AsyncAutoResetEvent heightFourStepChangedToPropose = new AsyncAutoResetEvent();
            consensusContext.StateChanged += (_, eventArgs) =>
            {
                if (eventArgs.Height == 4 && eventArgs.Step == ConsensusStep.Propose)
                {
                    heightFourStepChangedToPropose.Set();
                }
            };
            consensusContext.MessagePublished += (_, eventArgs) =>
            {
                if (eventArgs.Message is ConsensusProposalMsg proposalMsg)
                {
                    proposal = proposalMsg;

                    if (proposal.Height == 3L)
                    {
                        proposalMessageHeightThreeSent.Set();
                    }
                    else if (proposal.Height == 7L)
                    {
                        proposalMessageHeightSevenSent.Set();
                    }
                }
            };

            var block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            var blockCommit = TestUtils.CreateBlockCommit(block);
            blockChain.Append(block, blockCommit);
            block = blockChain.ProposeBlock(TestUtils.PrivateKeys[2], blockCommit);
            blockChain.Append(block, TestUtils.CreateBlockCommit(block));

            await proposalMessageHeightThreeSent.WaitAsync();
            Assert.NotNull(proposal?.BlockHash);
            var blockHash = proposal!.BlockHash;

            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[0],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[0],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[1],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[2],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));

            // Next height starts normally.
            await heightFourStepChangedToPropose.WaitAsync();
            Assert.Equal(4, consensusContext.Height);
            Assert.Equal(0, consensusContext.Round);

            blockCommit = blockChain.GetBlockCommit(blockChain.Tip.Hash);
            block = blockChain.ProposeBlock(TestUtils.PrivateKeys[0], blockCommit);
            blockCommit = TestUtils.CreateBlockCommit(block);
            blockChain.Append(block, blockCommit);
            block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1], blockCommit);
            blockCommit = TestUtils.CreateBlockCommit(block);
            blockChain.Append(block, blockCommit);
            block = blockChain.ProposeBlock(TestUtils.PrivateKeys[2], blockCommit);
            blockCommit = TestUtils.CreateBlockCommit(block);
            blockChain.Append(block, blockCommit);

            await proposalMessageHeightSevenSent.WaitAsync();
            Assert.NotNull(proposal?.BlockHash);
            var heightFourBlock = BlockMarshaler.UnmarshalBlock(
                (Dictionary)_codec.Decode(proposal!.Proposal.MarshaledBlock));
            Assert.Single(heightFourBlock.Evidences);
        }

        [Fact(Timeout = Timeout)]
        public async void IgnoreDifferentHeightVote()
        {
            ConsensusProposalMsg? proposal = null;
            var proposalMessageSent = new AsyncAutoResetEvent();
            var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
                TimeSpan.FromSeconds(1),
                TestUtils.Policy,
                actionLoader: null,
                TestUtils.PrivateKeys[3]);

            AsyncAutoResetEvent heightThreeStepChangedToPropose = new AsyncAutoResetEvent();
            consensusContext.MessagePublished += (_, eventArgs) =>
            {
                if (eventArgs.Message is ConsensusProposalMsg proposalMsg)
                {
                    proposal = proposalMsg;
                    proposalMessageSent.Set();
                }
            };

            var block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            var blockCommit = TestUtils.CreateBlockCommit(block);
            blockChain.Append(block, blockCommit);
            block = blockChain.ProposeBlock(TestUtils.PrivateKeys[2], blockCommit);
            blockChain.Append(block, TestUtils.CreateBlockCommit(block));

            await proposalMessageSent.WaitAsync();
            Assert.NotNull(proposal?.BlockHash);
            var blockHash = proposal!.BlockHash;

            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[0],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[0],
                power: BigInteger.One,
                height: 4,
                round: 0,
                hash: new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[1],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[2],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));

            Assert.Empty(consensusContext.Contexts[3].GetDuplicatedVotePairs());
        }

        [Fact(Timeout = Timeout)]
        public async void IgnoreDifferentRoundVote()
        {
            ConsensusProposalMsg? proposal = null;
            var proposalMessageSent = new AsyncAutoResetEvent();
            var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
                TimeSpan.FromSeconds(1),
                TestUtils.Policy,
                actionLoader: null,
                TestUtils.PrivateKeys[3]);

            AsyncAutoResetEvent heightThreeStepChangedToPropose = new AsyncAutoResetEvent();
            consensusContext.MessagePublished += (_, eventArgs) =>
            {
                if (eventArgs.Message is ConsensusProposalMsg proposalMsg)
                {
                    proposal = proposalMsg;
                    proposalMessageSent.Set();
                }
            };

            var block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            var blockCommit = TestUtils.CreateBlockCommit(block);
            blockChain.Append(block, blockCommit);
            block = blockChain.ProposeBlock(TestUtils.PrivateKeys[2], blockCommit);
            blockChain.Append(block, TestUtils.CreateBlockCommit(block));

            await proposalMessageSent.WaitAsync();
            Assert.NotNull(proposal?.BlockHash);
            var blockHash = proposal!.BlockHash;

            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[0],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[0],
                power: BigInteger.One,
                height: 3,
                round: 1,
                hash: new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[1],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[2],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));

            Assert.Empty(consensusContext.Contexts[3].GetDuplicatedVotePairs());
        }

        [Fact(Timeout = Timeout)]
        public async void IgnoreDifferentFlagVote()
        {
            ConsensusProposalMsg? proposal = null;
            var proposalMessageSent = new AsyncAutoResetEvent();
            var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
                TimeSpan.FromSeconds(1),
                TestUtils.Policy,
                actionLoader: null,
                TestUtils.PrivateKeys[3]);

            AsyncAutoResetEvent heightThreeStepChangedToPropose = new AsyncAutoResetEvent();
            consensusContext.MessagePublished += (_, eventArgs) =>
            {
                if (eventArgs.Message is ConsensusProposalMsg proposalMsg)
                {
                    proposal = proposalMsg;
                    proposalMessageSent.Set();
                }
            };

            var block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            var blockCommit = TestUtils.CreateBlockCommit(block);
            blockChain.Append(block, blockCommit);
            block = blockChain.ProposeBlock(TestUtils.PrivateKeys[2], blockCommit);
            blockChain.Append(block, TestUtils.CreateBlockCommit(block));

            await proposalMessageSent.WaitAsync();
            Assert.NotNull(proposal?.BlockHash);
            var blockHash = proposal!.BlockHash;

            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[0],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreVoteMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[0],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: new BlockHash(TestUtils.GetRandomBytes(BlockHash.Size)),
                flag: VoteFlag.PreVote)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[1],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[2],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));

            Assert.Empty(consensusContext.Contexts[3].GetDuplicatedVotePairs());
        }

        [Fact(Timeout = Timeout)]
        public async void IgnoreSameBlockHashVote()
        {
            ConsensusProposalMsg? proposal = null;
            var proposalMessageSent = new AsyncAutoResetEvent();
            var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
                TimeSpan.FromSeconds(1),
                TestUtils.Policy,
                actionLoader: null,
                TestUtils.PrivateKeys[3]);

            AsyncAutoResetEvent heightThreeStepChangedToPropose = new AsyncAutoResetEvent();
            consensusContext.MessagePublished += (_, eventArgs) =>
            {
                if (eventArgs.Message is ConsensusProposalMsg proposalMsg)
                {
                    proposal = proposalMsg;
                    proposalMessageSent.Set();
                }
            };

            var block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            var blockCommit = TestUtils.CreateBlockCommit(block);
            blockChain.Append(block, blockCommit);
            block = blockChain.ProposeBlock(TestUtils.PrivateKeys[2], blockCommit);
            blockChain.Append(block, TestUtils.CreateBlockCommit(block));

            await proposalMessageSent.WaitAsync();
            Assert.NotNull(proposal?.BlockHash);
            var blockHash = proposal!.BlockHash;

            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[0],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[0],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[1],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[2],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: blockHash,
                flag: VoteFlag.PreCommit)));

            Assert.Empty(consensusContext.Contexts[3].GetDuplicatedVotePairs());
        }

        [Fact(Timeout = Timeout)]
        public async void IgnoreNillVote()
        {
            ConsensusProposalMsg? proposal = null;
            var proposalMessageSent = new AsyncAutoResetEvent();
            var (blockChain, consensusContext) = TestUtils.CreateDummyConsensusContext(
                TimeSpan.FromSeconds(1),
                TestUtils.Policy,
                actionLoader: null,
                TestUtils.PrivateKeys[3]);

            AsyncAutoResetEvent heightThreeStepChangedToPropose = new AsyncAutoResetEvent();
            consensusContext.MessagePublished += (_, eventArgs) =>
            {
                if (eventArgs.Message is ConsensusProposalMsg proposalMsg)
                {
                    proposal = proposalMsg;
                    proposalMessageSent.Set();
                }
            };

            var block = blockChain.ProposeBlock(TestUtils.PrivateKeys[1]);
            var blockCommit = TestUtils.CreateBlockCommit(block);
            blockChain.Append(block, blockCommit);
            block = blockChain.ProposeBlock(TestUtils.PrivateKeys[2], blockCommit);
            blockChain.Append(block, TestUtils.CreateBlockCommit(block));

            await proposalMessageSent.WaitAsync();
            Assert.NotNull(proposal?.BlockHash);

            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[0],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: proposal!.BlockHash,
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[0],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: default,
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[1],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: proposal!.BlockHash,
                flag: VoteFlag.PreCommit)));
            consensusContext.HandleMessage(new ConsensusPreCommitMsg(TestUtils.CreateVote(
                privateKey: TestUtils.PrivateKeys[2],
                power: BigInteger.One,
                height: 3,
                round: 0,
                hash: proposal!.BlockHash,
                flag: VoteFlag.PreCommit)));

            Assert.Empty(consensusContext.Contexts[3].GetDuplicatedVotePairs());
        }
    }
}
