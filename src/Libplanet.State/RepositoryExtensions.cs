using Libplanet.Data;
using Libplanet.Types;

namespace Libplanet.State;

public static class RepositoryExtensions
{
    public static void AppendExecution(this Repository @this, BlockExecutionInfo blockExecution)
    {
        // @this.StateRootHash = blockExecution.StateRootHash;
        // @this.StateRootHashes.Add(blockExecution.Block.BlockHash, blockExecution.StateRootHash);
        // @this.TxExecutions.AddRange(blockExecution.GetTxExecutions(blockExecution.Block.BlockHash));
    }
}
