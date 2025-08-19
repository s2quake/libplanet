using Libplanet.Net.Consensus;
using Libplanet.Types;
using static Libplanet.Net.Tests.TestUtils;

namespace Libplanet.Net.Tests.Consensus;

public static class ConsensusExtensions
{
    public static async Task<Proposal> ProposeAsync(
        this Net.Consensus.Consensus @this,
        int index,
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
        }.Create(Signers[index]);
        await @this.ProposeAsync(proposal, cancellationToken);
        return proposal;
    }

    public static Task PreVoteAsync(
        this Net.Consensus.Consensus @this,
        int index,
        Block block,
        int round = 0,
        CancellationToken cancellationToken = default)
    {
        var preVote = new VoteBuilder
        {
            Validator = Validators[index],
            Block = block,
            Round = round,
            Type = VoteType.PreVote,
        }.Create(Signers[index]);
        return @this.PreVoteAsync(preVote, cancellationToken);
    }
}
