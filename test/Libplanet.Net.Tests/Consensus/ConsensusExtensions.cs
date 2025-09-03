using Libplanet.Extensions;
using Libplanet.Net.Consensus;
using Libplanet.Types;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public static class ConsensusExtensions
{
    public static async Task<Proposal> ProposeAsync(
        this Net.Consensus.Consensus @this,
        int validator,
        Block block,
        int round = 0,
        int validRound = -1,
        CancellationToken cancellationToken = default)
    {
        var proposal = new ProposalBuilder
        {
            Block = block,
            Round = round,
            ValidRound = validRound,
        }.Create(Signers[validator]);
        await @this.ProposeAsync(proposal, cancellationToken);
        return proposal;
    }

    public static async Task<Vote> PreVoteAsync(
        this Net.Consensus.Consensus @this,
        int validator,
        Block block,
        int round = 0,
        CancellationToken cancellationToken = default)
    {
        var preVote = new VoteBuilder
        {
            Validator = Validators[validator],
            Block = block,
            Round = round,
            Type = VoteType.PreVote,
        }.Create(Signers[validator]);
        await @this.PreVoteAsync(preVote, cancellationToken);
        return preVote;
    }

    public static async Task<Vote> NilPreVoteAsync(
        this Net.Consensus.Consensus @this,
        int validator,
        int height,
        int round = 0,
        CancellationToken cancellationToken = default)
    {
        var preVote = new NilVoteBuilder
        {
            Validator = Validators[validator],
            Height = height,
            Round = round,
            Type = VoteType.PreVote,
        }.Create(Signers[validator]);
        await @this.PreVoteAsync(preVote, cancellationToken);
        return preVote;
    }

    public static async Task<Vote> PreCommitAsync(
        this Net.Consensus.Consensus @this,
        int validator,
        Block block,
        int round = 0,
        CancellationToken cancellationToken = default)
    {
        var preCommit = new VoteBuilder
        {
            Validator = Validators[validator],
            Block = block,
            Round = round,
            Type = VoteType.PreCommit,
        }.Create(Signers[validator]);
        await @this.PreCommitAsync(preCommit, cancellationToken);
        return preCommit;
    }

    public static async Task<Vote> NilPreCommitAsync(
        this Net.Consensus.Consensus @this,
        int validator,
        int height,
        int round = 0,
        CancellationToken cancellationToken = default)
    {
        var preCommit = new NilVoteBuilder
        {
            Validator = Validators[validator],
            Height = height,
            Round = round,
            Type = VoteType.PreCommit,
        }.Create(Signers[validator]);
        await @this.PreCommitAsync(preCommit, cancellationToken);
        return preCommit;
    }

    public static async Task<(ConsensusStep, BlockHash)> WaitStepAsync(
        this Net.Consensus.Consensus @this,
        ConsensusStep step,
        CancellationToken cancellationToken = default)
        => await @this.StepChanged.WaitAsync(e => e.Step == step, cancellationToken);

    public static async Task<(ConsensusStep, BlockHash)> WaitStepAsync(
        this Net.Consensus.Consensus @this,
        ConsensusStep step,
        int round,
        CancellationToken cancellationToken = default)
        => await @this.StepChanged.WaitAsync(e => e.Step == step && @this.Round.Index == round, cancellationToken);
}
