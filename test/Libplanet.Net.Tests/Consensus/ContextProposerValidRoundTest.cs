using Libplanet.Extensions;
using Libplanet.Net.Consensus;
using Libplanet.Tests;
using Libplanet.TestUtilities;
using Libplanet.Types;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public sealed class ContextProposerValidRoundTest(ITestOutputHelper output)
{
    [Fact(Timeout = TestUtils.Timeout)]
    public async Task EnterValidRoundPreVoteBlock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var proposeStep2Task = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.Propose && consensus.Round.Index == 2);
        var timeoutTask = consensus.TimeoutOccurred.WaitAsync(
            e => e == ConsensusStep.Propose && consensus.Round.Index == 2);

        var block = blockchain.Propose(Signers[1]);

        await consensus.StartAsync(cancellationToken);
        await consensus.ProposeAsync(1, block, cancellationToken: cancellationToken);

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
        await Assert.ThrowsAsync<TimeoutException>(() => timeoutTask.WaitAsync(WaitTimeout5, cancellationToken));
        Assert.Equal(ConsensusStep.PreVote, consensus.Step);
    }

    [Fact(Timeout = 60000)]
    public async Task EnterValidRoundPreVoteNil()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var random = RandomUtility.GetRandom(output);
        var proposer = RandomUtility.Signer(random);
        var genesisBlock = TestUtils.GenesisBlockBuilder.Create(proposer);
        var blockchain = new Blockchain(genesisBlock);
        await using var consensus = new Net.Consensus.Consensus(Validators);
        var timeoutTask = consensus.TimeoutOccurred.WaitAsync();
        var proposeStep2Task = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.Propose && consensus.Round.Index == 2);
        var proposeStep3Task = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.Propose && consensus.Round.Index == 3);
        var preCommitStep2Task = consensus.StepChanged.WaitAsync(
            e => e.Step == ConsensusStep.PreCommit && consensus.Round.Index == 2);

        var block = blockchain.Propose(Signers[1]);
        var signer = RandomUtility.Signer(random);
        var differentBlock = new RawBlock
        {
            Header = new BlockHeader
            {
                BlockVersion = BlockHeader.CurrentProtocolVersion,
                Height = blockchain.Tip.Height + 1,
                Timestamp = blockchain.Tip.Timestamp.Add(TimeSpan.FromSeconds(1)),
                Proposer = signer.Address,
                PreviousBlockHash = blockchain.Tip.BlockHash,
            },
        }.Sign(signer);

        await consensus.StartAsync(cancellationToken);
        await consensus.ProposeAsync(1, block, cancellationToken: cancellationToken);
        Assert.NotNull(consensus.Proposal);
        Block proposedBlock = consensus.Proposal.Block;

        _ = consensus.PreVoteAsync(1, proposedBlock, cancellationToken: cancellationToken);

        // Force round change to 2.
        foreach (var i in new int[] { 0, 2 })
        {
            _ = consensus.PreVoteAsync(i, proposedBlock, round: 2, cancellationToken);
        }

        await proposeStep2Task.WaitAsync(cancellationToken);
        Assert.Equal(2, consensus.Round.Index);
        // Assert no transition is due to timeout.
        await Assert.ThrowsAsync<TimeoutException>(
            () => timeoutTask.WaitAsync(TimeSpan.FromMilliseconds(1), cancellationToken));

        // Updated locked round and valid round to 2.
        _ = consensus.ProposeAsync(3, proposedBlock, round: 2, cancellationToken: cancellationToken);
        foreach (var i in new int[] { 1, 3 })
        {
            _ = consensus.PreVoteAsync(i, proposedBlock, round: 2, cancellationToken);
        }

        await preCommitStep2Task.WaitAsync(cancellationToken);

        // Force round change to 3.
        foreach (var i in new int[] { 0, 2 })
        {
            _ = consensus.PreVoteAsync(i, differentBlock, round: 3, cancellationToken);
        }

        await proposeStep3Task.WaitAsync(cancellationToken);
        Assert.Equal(3, consensus.Round.Index);

        _ = consensus.ProposeAsync(0, differentBlock, round: 3, validRound: 0, cancellationToken);
        await consensus.PreVoteAsync(3, differentBlock, round: 3, cancellationToken);
        await Assert.ThrowsAsync<TimeoutException>(
            () => timeoutTask.WaitAsync(TimeSpan.FromMilliseconds(1), cancellationToken));
    }
}
