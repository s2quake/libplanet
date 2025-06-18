using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public class InvalidVoteException : Exception
{
    public InvalidVoteException(string message, Vote vote, Exception innerException)
        : base(message, innerException)
    {
        Vote = vote;
    }

    public InvalidVoteException(string message, Vote vote)
        : base(message)
    {
        Vote = vote;
    }

    public Vote Vote { get; }
}
