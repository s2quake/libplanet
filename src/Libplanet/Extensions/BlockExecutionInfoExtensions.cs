using Libplanet.State;
using Libplanet.Types;

namespace Libplanet.Extensions;

public static class BlockExecutionInfoExtensions
{
    public static TxExecution[] GetTxExecutions(this BlockExecutionInfo @this, BlockHash blockHash)
    {
        return [.. @this.Executions.Select(ToTxExecution)];

        TxExecution ToTxExecution(TransactionExecutionInfo evaluation) => new()
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

        static IEnumerable<ActionExecutionInfo> GetActionEvaluations(TransactionExecutionInfo txEvaluation)
            => txEvaluation.EnterExecutions.Concat(txEvaluation.Executions).Concat(txEvaluation.LeaveExecutions);

        static string GetExceptionName(ActionExecutionInfo evaluation)
            => GetActualException(evaluation.Exception)?.GetType().FullName ?? string.Empty;

        static Exception? GetActualException(Exception? exception) => exception?.InnerException ?? exception;
    }

    public static BlockExecution GetBlockExecution(this BlockExecutionInfo @this, BlockHash blockHash) => new()
    {
        BlockHash = blockHash,
        EnterState = @this.EnterWorld.Hash,
        LeaveState = @this.LeaveWorld.Hash,
    };
}
