#pragma warning disable SA1402 // File may only contain a single type
namespace Libplanet.Types.Evidence;

public abstract class EvidenceException : Exception
{
    protected EvidenceException(string message)
        : base(message)
    {
    }

    protected EvidenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public abstract long Height { get; }

    public abstract EvidenceBase Create(EvidenceContext evidenceContext);
}

public abstract class EvidenceException<T> : EvidenceException
    where T : EvidenceBase
{
    protected EvidenceException(string message)
        : base(message)
    {
    }

    protected EvidenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public sealed override EvidenceBase Create(EvidenceContext evidenceContext)
        => CreateEvidence(evidenceContext);

    public abstract T CreateEvidence(EvidenceContext evidenceContext);
}
