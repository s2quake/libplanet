namespace Libplanet.Net.Consensus;

internal sealed record class RoundVoteSet
{
    public required VoteCollection PreVotes { get; init; }

    public required VoteCollection PreCommits { get; init; }

    public int Count => PreVotes.Count + PreCommits.Count;
}
