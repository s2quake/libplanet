using Libplanet.Types.Blocks;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Store;

public static class DictionaryExtensions
{
    public static void Add(this IDictionary<EvidenceId, EvidenceBase> @this, EvidenceBase evidence)
        => @this.Add(evidence.Id, evidence);

    public static void Add(this IDictionary<TxId, Transaction> @this, Transaction transaction)
        => @this.Add(transaction.Id, transaction);

    public static void Add(this IDictionary<BlockHash, BlockCommit> @this, BlockCommit blockCommits)
        => @this.Add(blockCommits.BlockHash, blockCommits);

    public static void Remove(this IDictionary<EvidenceId, EvidenceBase> @this, EvidenceBase evidence)
        => @this.Remove(evidence.Id);

    public static void Remove(this IDictionary<BlockHash, BlockCommit> @this, BlockCommit blockCommit)
        => @this.Remove(blockCommit.BlockHash);
}
