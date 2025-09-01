using Libplanet.Types;

namespace Libplanet.State;

public static class BlockExecutionExtensions
{
    public static TxExecutionInfo[] GetTxExecutions(this BlockExecution @this, BlockHash blockHash)
    {
        return [.. @this.Executions.Select(ToTxExecution)];

        TxExecutionInfo ToTxExecution(TransactionExecution evaluation) => new()
        {
            TxId = evaluation.Transaction.Id,
            BlockHash = blockHash,
            EnterState = evaluation.EnterWorld.Hash,
            LeaveState = evaluation.LeaveWorld.Hash,
            ExceptionNames =
            [
                .. GetActionEvaluations(evaluation).Select(GetExceptionName).Where(item => item != string.Empty),
            ],
        };

        static IEnumerable<ActionExecution> GetActionEvaluations(TransactionExecution txEvaluation)
            => txEvaluation.EnterExecutions.Concat(txEvaluation.Executions).Concat(txEvaluation.LeaveExecutions);

        static string GetExceptionName(ActionExecution evaluation)
            => GetActualException(evaluation.Exception)?.GetType().FullName ?? string.Empty;

        static Exception? GetActualException(Exception? exception) => exception?.InnerException ?? exception;
    }

    public static BlockExecutionResult GetBlockExecution(this BlockExecution @this, BlockHash blockHash) => new()
    {
        BlockHash = blockHash,
        EnterState = @this.EnterWorld.Hash,
        LeaveState = @this.LeaveWorld.Hash,
    };
}
