using Libplanet.Extensions;
using Libplanet.Net.Consensus;
using Libplanet.Net.Messages;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using Nito.AsyncEx;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public class ContextProposerValidRoundTest
{
    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterValidRoundPreVoteBlock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var proposeStep2Task = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.Propose && consensus.Round.Index == 2);
        var timeoutTask = consensus.TimeoutOccurred.WaitAsync(
            e => e == ConsensusStep.Propose && consensus.Round.Index == 2);

        var block = blockchain.ProposeBlock(Signers[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
        }.Create(Signers[1]);

        await consensus.StartAsync(cancellationToken);
        await consensus.ProposeAsync(proposal, cancellationToken);

        // Force round change.
        foreach (var i in new int[] { 0, 2 })
        {
            _ = consensus.PreVoteAsync(i, block, round: 2, cancellationToken);
        }

        await proposeStep2Task.WaitAsync(cancellationToken);
        Assert.Equal(2, consensus.Round.Index);

        _ = consensus.ProposeAsync(3, block, round: 2, validRound: 1, cancellationToken);

        foreach (var i in new int[] { 0, 1, 2, 3 })
        {
            _ = consensus.PreVoteAsync(i, block, round: 1, cancellationToken);
        }

        await proposeStep2Task.WaitAsync(cancellationToken);
        // Assert no transition is due to timeout.
        await Assert.ThrowsAsync<TimeoutException>(() => timeoutTask.WaitAsync(WaitTimeout, cancellationToken));
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
    }

    [Fact(Timeout = 60000)]
    public async Task EnterValidRoundPreVoteNil()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var timeoutTask = consensus.TimeoutOccurred.WaitAsync();
        var proposeStep2Task = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.Propose && consensus.Round.Index == 2);
        var proposeStep3Task = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.Propose && consensus.Round.Index == 3);
        var preCommitStep2Task = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.PreCommit && consensus.Round.Index == 2);
        var preVoteStep3Task = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.PreVote && consensus.Round.Index == 3);

        var block = blockchain.ProposeBlock(Signers[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
        }.Create(Signers[1]);
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
                BlockVersion = BlockHeader.CurrentProtocolVersion,
                Height = blockchain.Tip.Height + 1,
                Timestamp = blockchain.Tip.Timestamp.Add(TimeSpan.FromSeconds(1)),
                Proposer = key.Address,
                PreviousHash = blockchain.Tip.BlockHash,
            },
        }.Sign(key);

        await consensus.StartAsync(cancellationToken);
        await consensus.ProposeAsync(proposal, cancellationToken);
        Assert.NotNull(consensus.Proposal);
        Block proposedBlock = consensus.Proposal.Block;

        // Force round change to 2.
        foreach (var i in new int[] { 0, 2 })
        {
            _ = consensus.PreVoteAsync(
                new VoteBuilder
                {
                    Validator = Validators[i],
                    Block = proposedBlock,
                    Round = 2,
                    Type = VoteType.PreVote,
                }.Create(Signers[i]), cancellationToken);
        }
        // consensus.ProduceMessage(
        //     new ConsensusPreVoteMessage
        //     {
        //         PreVote = new VoteBuilder
        //         {
        //             Validator = Validators[0],
        //             Block = proposedBlock,
        //             Round = 2,
        //             Type = VoteType.PreVote,
        //         }.Create(Signers[0])
        //     });
        // consensus.ProduceMessage(
        //     new ConsensusPreVoteMessage
        //     {
        //         PreVote = new VoteBuilder
        //         {
        //             Validator = Validators[2],
        //             Block = proposedBlock,
        //             Round = 2,
        //             Type = VoteType.PreVote,
        //         }.Create(Signers[2])
        //     });
        // await stateChangedToRoundTwoPropose.WaitAsync();
        await proposeStep2Task.WaitAsync(cancellationToken);
        Assert.Equal(2, consensus.Round.Index);
        // Assert no transition is due to timeout.
        await Assert.ThrowsAsync<TimeoutException>(
            () => timeoutTask.WaitAsync(TimeSpan.FromMilliseconds(1), cancellationToken));
        // Assert.False(timeoutProcessed); // Assert no transition is due to timeout.

        // Updated locked round and valid round to 2.
        var proposal2 = new ProposalBuilder
        {
            Block = proposedBlock,
            Round = 2,
            ValidRound = 1,
        }.Create(Signers[3]);
        _ = consensus.ProposeAsync(proposal2, cancellationToken);
        // consensus.ProduceMessage(
        //     CreateConsensusPropose(
        //         proposedBlock,
        //         Signers[3],
        //         height: 1,
        //         round: 2,
        //         validRound: -1));

        foreach (var i in new int[] { 1, 3 })
        {
            _ = consensus.PreVoteAsync(
                new VoteBuilder
                {
                    Validator = Validators[i],
                    Block = proposedBlock,
                    Round = 2,
                    Type = VoteType.PreVote,
                }.Create(Signers[i]), cancellationToken);
        }

        // consensus.ProduceMessage(
        //     new ConsensusPreVoteMessage
        //     {
        //         PreVote = new VoteBuilder
        //         {
        //             Validator = Validators[3],
        //             Block = proposedBlock,
        //             Round = 2,
        //             Type = VoteType.PreVote,
        //         }.Create(Signers[3])
        //     });
        // await stateChangedToRoundTwoPreCommit.WaitAsync();
        await preCommitStep2Task.WaitAsync(cancellationToken);

        // Force round change to 3.
        foreach (var i in new int[] { 0, 2 })
        {
            _ = consensus.PreVoteAsync(
                new VoteBuilder
                {
                    Validator = Validators[i],
                    Block = differentBlock,
                    Round = 3,
                    Type = VoteType.PreVote,
                }.Create(Signers[i]), cancellationToken);
        }
        // consensus.ProduceMessage(
        //     new ConsensusPreVoteMessage
        //     {
        //         PreVote = new VoteBuilder
        //         {
        //             Validator = Validators[0],
        //             Block = differentBlock,
        //             Round = 3,
        //             Type = VoteType.PreVote,
        //         }.Create(Signers[0])
        //     });
        // consensus.ProduceMessage(
        //     new ConsensusPreVoteMessage
        //     {
        //         PreVote = new VoteBuilder
        //         {
        //             Validator = Validators[2],
        //             Block = differentBlock,
        //             Round = 3,
        //             Type = VoteType.PreVote,
        //         }.Create(Signers[2])
        //     });
        // await stateChangedToRoundThreePropose.WaitAsync();
        await proposeStep3Task.WaitAsync(cancellationToken);
        Assert.Equal(3, consensus.Round.Index);

        var proposal3 = new ProposalBuilder
        {
            Block = differentBlock,
            Round = 3,
            ValidRound = 0,
        }.Create(Signers[0]);
        _ = consensus.ProposeAsync(proposal3, cancellationToken);
        // consensus.ProduceMessage(CreateConsensusPropose(
        //     differentBlock, Signers[0], round: 3, validRound: 0));
        _ = consensus.PreVoteAsync(
            new VoteBuilder
            {
                Validator = Validators[3],
                Block = differentBlock,
                Round = 3,
                Type = VoteType.PreVote,
            }.Create(Signers[3]), cancellationToken);
        // consensus.ProduceMessage(
        //     new ConsensusPreVoteMessage
        //     {
        //         PreVote = new VoteBuilder
        //         {
        //             Validator = Validators[3],
        //             Block = differentBlock,
        //             Round = 3,
        //             Type = VoteType.PreVote,
        //         }.Create(Signers[3])
        //     });

        // await roundThreeNilPreVoteSent.WaitAsync();
        var (_, actualBlockHash) = await preVoteStep3Task.WaitAsync(cancellationToken);
        await Assert.ThrowsAsync<TimeoutException>(
            () => timeoutTask.WaitAsync(TimeSpan.FromMilliseconds(1), cancellationToken));
        // Assert.False(timeoutProcessed);
        Assert.Equal(default, actualBlockHash);
    }
}
