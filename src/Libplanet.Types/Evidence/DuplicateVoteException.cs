using Libplanet.Types.Consensus;

namespace Libplanet.Types.Evidence;

public sealed class DuplicateVoteException : EvidenceException
{
    public DuplicateVoteException(
        string message,
        Vote voteRef,
        Vote voteDup,
        Exception innerException)
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

    protected override EvidenceBase OnCreateEvidence(IEvidenceContext evidenceContext)
    {
        var voteRef = VoteRef;
        var voteDup = VoteDup;
        (_, Vote dup) = DuplicateVoteEvidence.OrderDuplicateVotePair(voteRef, voteDup);
        var validator = evidenceContext.Validators.GetValidator(voteRef.ValidatorPublicKey);

        return DuplicateVoteEvidence.Create(
            voteRef,
            voteDup,
            evidenceContext.Validators,
            dup.Timestamp);
    }
}
