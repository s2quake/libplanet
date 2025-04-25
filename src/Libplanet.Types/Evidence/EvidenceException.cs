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

    public EvidenceBase CreateEvidence(IEvidenceContext evidenceContext)
    {
        var evidence = OnCreateEvidence(evidenceContext);
        if (evidence is null)
        {
            var message = $"{nameof(OnCreateEvidence)} must return a non-null " +
                          $"instance of {nameof(EvidenceBase)}.";
            throw new InvalidOperationException(message);
        }

        return evidence;
    }

    protected abstract EvidenceBase OnCreateEvidence(IEvidenceContext evidenceContext);
}
