using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public static class DictionaryExtensions
{
    public static void Add(this IDictionary<EvidenceId, EvidenceBase> @this, EvidenceBase evidence)
        => @this.Add(evidence.Id, evidence);

    public static void Add(this IDictionary<TxId, Transaction> @this, Transaction transaction)
        => @this.Add(transaction.Id, transaction);

    public static void Remove(this IDictionary<EvidenceId, EvidenceBase> @this, EvidenceBase evidence)
        => @this.Remove(evidence.Id);
}
