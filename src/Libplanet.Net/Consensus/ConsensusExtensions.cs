using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public static class ConsensusExtensions
{
    public static Task ProposeAsync(this Consensus @this, Proposal proposal)
        => @this.ProposeAsync(proposal, default);
    
    public static Task PreVoteAsync(this Consensus @this, Vote vote)
        => @this.PreVoteAsync(vote, default);

    public static Task PreCommitAsync(this Consensus @this, Vote vote)
        => @this.PreCommitAsync(vote, default);
}
