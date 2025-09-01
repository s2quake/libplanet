using Libplanet.Data;

namespace Libplanet.State;

public static class RepositoryExtensions
{
    public static void AppendExecution(this Repository @this, BlockExecution execution)
    {
        @this.StateRootHashes.Add(execution.Block.BlockHash, execution.LeaveWorld.Hash);
        @this.TxExecutions.AddRange(execution.GetTxExecutions(execution.Block.BlockHash));
        @this.StateRootHash = execution.LeaveWorld.Hash;
    }
}
