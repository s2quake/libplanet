namespace Libplanet.Net.Consensus;

internal sealed record class RoundVoteSet(VoteSet PreVotes, VoteSet PreCommits)
{
    public int Count => PreVotes.TotalCount + PreCommits.TotalCount;
}

