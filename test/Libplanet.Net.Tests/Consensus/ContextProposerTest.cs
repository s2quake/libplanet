using Libplanet.Extensions;
using Libplanet.Net.Consensus;
using Libplanet.Tests;
using Libplanet.TestUtilities.Extensions;
using Libplanet.Types;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public sealed class ContextProposerTest
{
    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreCommitNil()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var preCommitStepTask = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.PreCommit);

        var block = blockchain.ProposeBlock(Signers[1]);

        await consensus.StartAsync(cancellationToken);

        _ = consensus.ProposeAsync(validator: 1, block, cancellationToken: cancellationToken);
        _ = consensus.NilPreVoteAsync(validator: 1, height: 1, cancellationToken: cancellationToken);

        foreach (var i in new int[] { 0, 2, 3 })
        {
            _ = consensus.NilPreVoteAsync(validator: i, height: 1, cancellationToken: cancellationToken);
        }

        var (_, actualBlockHash) = await preCommitStepTask.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(default, actualBlockHash);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreCommitBlock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var preCommitStepTask = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.PreCommit && consensus.Round.Index == 0);
        var block = blockchain.ProposeBlock(Signers[1]);

        await consensus.StartAsync(cancellationToken);

        await consensus.ProposeAsync(
            new ProposalBuilder
            {
                Block = block,
            }.Create(Signers[1]),
            cancellationToken);

        // Wait for propose to process.
        Assert.NotNull(consensus.Proposal);
        var proposedblockHash = consensus.Proposal.BlockHash;

        foreach (var i in new int[] { 0, 1, 2, 3 })
        {
            _ = consensus.PreVoteAsync(
                new VoteBuilder
                {
                    Validator = Validators[i],
                    Block = block,
                    Type = VoteType.PreVote,
                }.Create(Signers[i]), cancellationToken);
        }

        var (_, actualBlockHash) = await preCommitStepTask.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(proposedblockHash, actualBlockHash);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
        Assert.Equal(ConsensusStep.PreCommit, consensus.Step);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterNewRoundNil()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var roundChanged1Task = consensus.RoundChanged.WaitAsync(e => e.Index == 1);
        var block = blockchain.ProposeBlock(Signers[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
        }.Create(Signers[1]);

        await consensus.StartAsync(cancellationToken);
        await consensus.ProposeAsync(proposal, cancellationToken);
        _ = consensus.PreCommitAsync(
            new VoteBuilder
            {
                Validator = Validators[1],
                Block = block,
                Type = VoteType.PreCommit,
            }.Create(Signers[1]), cancellationToken);
        foreach (var i in new int[] { 0, 2, 3 })
        {
            _ = consensus.PreCommitAsync(
                new NilVoteBuilder
                {
                    Validator = Validators[i],
                    Height = 1,
                    Type = VoteType.PreCommit,
                }.Create(Signers[i]), cancellationToken);
        }

        await roundChanged1Task.WaitAsync(cancellationToken);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(1, consensus.Round.Index);
        Assert.Equal(ConsensusStep.Propose, consensus.Step);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EndCommitBlock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var preCommitStepTask = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.PreCommit && consensus.Round.Index == 0);
        var endCommitStepTask = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.EndCommit && consensus.Round.Index == 0);
        var block = blockchain.ProposeBlock(Signers[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
        }.Create(Signers[1]);

        await consensus.StartAsync(cancellationToken);
        await consensus.ProposeAsync(proposal, cancellationToken);

        foreach (var i in new int[] { 0, 1, 2, 3 })
        {
            _ = consensus.PreVoteAsync(
                new VoteBuilder
                {
                    Validator = Validators[i],
                    Block = block,
                    Type = VoteType.PreVote,
                }.Create(Signers[i]), cancellationToken);
        }

        await preCommitStepTask.WaitAsync(cancellationToken);

        foreach (var i in new int[] { 0, 1, 2, 3 })
        {
            _ = consensus.PreCommitAsync(
                new VoteBuilder
                {
                    Validator = Validators[i],
                    Block = block,
                    Type = VoteType.PreCommit,
                }.Create(Signers[i]), cancellationToken);
        }

        await endCommitStepTask.WaitAsync(cancellationToken);

        Assert.Equal(proposal?.BlockHash, consensus.Round.PreCommits.GetBlockCommit().BlockHash);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
        Assert.Equal(ConsensusStep.EndCommit, consensus.Step);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreVoteNil()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators, height: 5); // Peer1 should be a proposer
        var preVoteStepTask = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.PreVote && consensus.Round.Index == 0);
        var block = blockchain.ProposeBlock(Signers[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
        }.Create(Signers[1]);

        await consensus.StartAsync(cancellationToken);
        await consensus.ProposeAsync(proposal, cancellationToken);

        var (_, actualBlockHash) = await preVoteStepTask.WaitAsync(WaitTimeout5, cancellationToken);
        Assert.Equal(default, actualBlockHash);
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
        Assert.Equal(5, consensus.Height);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterPreVoteBlock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var blockchain = MakeBlockchain();
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var preVoteStepTask = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.PreVote && consensus.Round.Index == 0);
        var block = blockchain.ProposeBlock(Signers[1]);
        var proposal = new ProposalBuilder
        {
            Block = block,
        }.Create(Signers[1]);

        await consensus.StartAsync(cancellationToken);
        await consensus.ProposeAsync(proposal, cancellationToken);

        var (_, actualBlockHash) = await preVoteStepTask.WaitAsync(cancellationToken);
        Assert.Equal(proposal.BlockHash, actualBlockHash);
        Assert.Equal(1, consensus.Height);
        Assert.Equal(0, consensus.Round.Index);
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
    }

    [Fact(Timeout = TestUtils.Timeout)]
    public async Task VoteNilOnSelfProposedInvalidBlock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var privateKey = new PrivateKey();
        var blockchain = MakeBlockchain();
        _ = blockchain.ProposeAndAppendMany(2);
        var block = blockchain.ProposeBlock(privateKey);
        var proposal = new ProposalBuilder
        {
            Block = block,
        }.Create(Signers[2]);

        await using var consensus = new Net.Consensus.Consensus(Validators, height: 2);
        var preVoteStepTask = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.PreVote && consensus.Round.Index == 0);

        await consensus.StartAsync(cancellationToken);
        await consensus.ProposeAsync(proposal, cancellationToken);
        var actualProposal = consensus.Proposal;
        Assert.NotNull(actualProposal);

        Assert.Equal(consensus.Height + 1, actualProposal.Height);
        var (_, actualBlockHash) = await preVoteStepTask.WaitAsync(cancellationToken);
        Assert.Equal(default, actualBlockHash);
    }
}
