namespace Libplanet.Net.Consensus;

internal sealed record class RoundVoteSet
{
    public required VoteSet PreVotes { get; init; }

    public required VoteSet PreCommits { get; init; }

    public int Count => PreVotes.TotalCount + PreCommits.TotalCount;
}
