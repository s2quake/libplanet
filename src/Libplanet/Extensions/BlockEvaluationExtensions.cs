using Libplanet.State;
using Libplanet.Types.Blocks;
using Libplanet.Types.Transactions;

namespace Libplanet.Extensions;

public static class BlockEvaluationExtensions
{
    public static TxExecution[] GetTxExecutions(this BlockExecutionInfo @this, BlockHash blockHash)
    {
        return [.. @this.Executions.Select(ToTxExecution)];

        TxExecution ToTxExecution(TransactionExecutionInfo evaluation) => new()
        {
            TxId = evaluation.Transaction.Id,
            BlockHash = blockHash,
            InputState = evaluation.InputWorld.Hash,
            OutputState = evaluation.OutputWorld.Hash,
            ExceptionNames = [.. GetActionEvaluations(evaluation).Select(GetExceptionName)],
        };

        static IEnumerable<ActionExecutionInfo> GetActionEvaluations(TransactionExecutionInfo txEvaluation)
            => txEvaluation.BeginExecutions.Concat(txEvaluation.Executions).Concat(txEvaluation.EndExecutions);

        static string GetExceptionName(ActionExecutionInfo evaluation)
            => GetActualException(evaluation.Exception)?.GetType().Name ?? string.Empty;

        static Exception? GetActualException(Exception? exception) => exception?.InnerException ?? exception;
    }

    public static BlockExecution GetBlockExecution(this BlockExecutionInfo @this, BlockHash blockHash) => new()
    {
        BlockHash = blockHash,
        InputState = @this.InputWorld.Hash,
        OutputState = @this.OutputWorld.Hash,
    };
}
