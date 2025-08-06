namespace Libplanet.Net.Consensus.Steps;

internal sealed class ProposePreVote(int round, VoteCollection votes)
{
    public VoteCollection Votes => votes;
}
