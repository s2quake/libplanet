using Libplanet.Types.Consensus;

namespace Libplanet.Types.Evidence;

public sealed class DuplicateVoteException : EvidenceException<DuplicateVoteEvidence>
{
    public DuplicateVoteException(string message, Vote voteRef, Vote voteDup, Exception innerException)
        : base(message, innerException)
    {
        VoteRef = voteRef;
        VoteDup = voteDup;
    }

    public DuplicateVoteException(string message, Vote voteRef, Vote voteDup)
        : base(message)
    {
        VoteRef = voteRef;
        VoteDup = voteDup;
    }

    public Vote VoteRef { get; }

    public Vote VoteDup { get; }

    public override long Height => VoteRef.Height;

    public override DuplicateVoteEvidence CreateEvidence(EvidenceContext evidenceContext)
        => DuplicateVoteEvidence.Create(VoteRef, VoteRef, evidenceContext.Validators);
}
