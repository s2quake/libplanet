using System.IO;
using Libplanet.Action.State;
using Libplanet.Types;
using Libplanet.Types.Evidence;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    public ImmutableArray<EvidenceBase> GetPendingEvidence()
        => Store.IteratePendingEvidenceIds()
                .OrderBy(id => id)
                .Select(id => Store.GetPendingEvidence(id))
                .OfType<EvidenceBase>()
                .ToImmutableArray();

    public EvidenceBase GetPendingEvidence(EvidenceId evidenceId)
    {
        if (Store.GetPendingEvidence(evidenceId) is EvidenceBase evidence)
        {
            return evidence;
        }

        throw new KeyNotFoundException($"Pending evidence {evidenceId} is not found.");
    }

    public EvidenceBase GetCommittedEvidence(EvidenceId evidenceId)
    {
        if (Store.GetCommittedEvidence(evidenceId) is EvidenceBase evidence)
        {
            return evidence;
        }

        throw new KeyNotFoundException($"Committed evidence {evidenceId} is not found.");
    }

    public void AddEvidence(EvidenceBase evidence)
    {
        if (IsEvidenceCommitted(evidence.Id))
        {
            throw new ArgumentException(
                message: $"Evidence {evidence.Id} is already committed.",
                paramName: nameof(evidence));
        }

        if (IsEvidencePending(evidence.Id))
        {
            throw new ArgumentException(
                message: $"Evidence {evidence.Id} is already pending.",
                paramName: nameof(evidence));
        }

        if (evidence.Height > Tip.Height)
        {
            throw new ArgumentException(
                message: $"Evidence {evidence.Id} is from the future: " +
                         $"{evidence.Height} > {Tip.Height + 1}",
                paramName: nameof(evidence));
        }

        if (IsEvidenceExpired(evidence))
        {
            throw new ArgumentException($"Evidence {evidence.Id} is expired");
        }

        var stateRootHash = GetNextStateRootHash(evidence.Height);
        var worldState = GetWorld(stateRootHash ?? default);
        var validators = worldState.GetValidatorSet();
        ValidationUtility.Validate(evidence, items: new Dictionary<object, object?>
        {
            [typeof(EvidenceContext)] = new EvidenceContext(validators),
        });
        Store.PutPendingEvidence(evidence);
    }

    public void CommitEvidence(EvidenceBase evidence)
    {
        if (IsEvidenceCommitted(evidence.Id))
        {
            throw new ArgumentException($"Evidence {evidence.Id} is already committed.");
        }

        if (IsEvidenceExpired(evidence))
        {
            throw new ArgumentException($"Evidence {evidence.Id} is expired");
        }

        if (IsEvidencePending(evidence.Id))
        {
            DeletePendingEvidence(evidence.Id);
        }

        Store.PutCommittedEvidence(evidence);
    }

    /// <summary>
    /// Check if <see cref="EvidenceBase"/> is on the <see cref="Store"/>'s pending pool.
    /// </summary>
    /// <param name="evidenceId"><see cref="EvidenceId"/> of the <see cref="EvidenceBase"/>
    /// to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if the <see cref="Store"/> contains <see cref="EvidenceBase"/>
    /// with the specified <paramref name="evidenceId"/> on the pending pool;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool IsEvidencePending(EvidenceId evidenceId)
        => Store.ContainsPendingEvidence(evidenceId);

    /// <summary>
    /// Check if <see cref="EvidenceBase"/> is on the <see cref="Store"/>'s committed pool.
    /// </summary>
    /// <param name="evidenceId"><see cref="EvidenceId"/> of the <see cref="EvidenceBase"/>
    /// to be checked.</param>
    /// <returns>
    /// <see langword="true"/> if the <see cref="Store"/> contains <see cref="EvidenceBase"/>
    /// with the specified <paramref name="evidenceId"/> on the committed pool;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool IsEvidenceCommitted(EvidenceId evidenceId)
        => Store.ContainsCommittedEvidence(evidenceId);

    /// <summary>
    /// Check if <see cref="EvidenceBase"/> is expired.
    /// </summary>
    /// <param name="evidence"><see cref="EvidenceBase"/> to check its expiry.</param>
    /// <returns>
    /// <see langword="true"/> if the <see cref="EvidenceBase"/> is expired;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool IsEvidenceExpired(EvidenceBase evidence)
        => evidence.Height + Options.MaxEvidencePendingDuration + evidence.Height < Tip.Height;

    /// <summary>
    /// Delete pending <see cref="EvidenceBase"/> from the <see cref="Store"/> with
    /// its <see cref="EvidenceBase.Id"/>.
    /// </summary>
    /// <param name="evidenceId"><see cref="EvidenceId"/> of the <see cref="EvidenceBase"/>
    /// to delete.</param>
    /// <returns>
    /// <see langword="true"/> if <see cref="EvidenceBase"/> is deleted from the pending pool.
    /// </returns>
    public bool DeletePendingEvidence(EvidenceId evidenceId)
    {
        if (IsEvidencePending(evidenceId))
        {
            Store.DeletePendingEvidence(evidenceId);
            return true;
        }

        return false;
    }
}
