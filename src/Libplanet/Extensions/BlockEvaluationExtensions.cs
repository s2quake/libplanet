using Libplanet.State;
using Libplanet.Types.Blocks;
using Libplanet.Types.Transactions;

namespace Libplanet.Extensions;

public static class BlockEvaluationExtensions
{
    public static TxExecution[] GetTxExecutions(this BlockResult @this, BlockHash blockHash)
    {
        return [.. @this.Evaluations.Select(ToTxExecution)];

        TxExecution ToTxExecution(TransactionResult evaluation) => new()
        {
            TxId = evaluation.Transaction.Id,
            BlockHash = blockHash,
            InputState = evaluation.InputWorld.Hash,
            OutputState = evaluation.OutputWorld.Hash,
            ExceptionNames = [.. GetActionEvaluations(evaluation).Select(GetExceptionName)],
        };

        static IEnumerable<ActionResult> GetActionEvaluations(TransactionResult txEvaluation)
            => txEvaluation.BeginEvaluations.Concat(txEvaluation.Evaluations).Concat(txEvaluation.EndEvaluations);

        static string GetExceptionName(ActionResult evaluation)
            => GetActualException(evaluation.Exception)?.GetType().Name ?? string.Empty;

        static Exception? GetActualException(Exception? exception) => exception?.InnerException ?? exception;
    }

    public static BlockExecution GetBlockExecution(this BlockResult @this, BlockHash blockHash) => new()
    {
        BlockHash = blockHash,
        InputState = @this.InputWorld.Hash,
        OutputState = @this.OutputWorld.Hash,
    };
}
