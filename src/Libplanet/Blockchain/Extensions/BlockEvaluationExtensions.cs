using Libplanet.Action;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain.Extensions;

public static class BlockEvaluationExtensions
{
    public static TxExecution[] GetTxExecutions(this BlockEvaluation @this, BlockHash blockHash)
    {
        return [.. @this.Evaluations.Select(ToTxExecution)];

        TxExecution ToTxExecution(TxEvaluation evaluation) => new()
        {
            TxId = evaluation.Transaction.Id,
            BlockHash = blockHash,
            InputState = evaluation.InputWorld.Trie.Hash,
            OutputState = evaluation.OutputWorld.Trie.Hash,
            ExceptionNames = [.. GetActionEvaluations(evaluation).Select(GetExceptionName)],
        };

        static IEnumerable<ActionEvaluation> GetActionEvaluations(TxEvaluation txEvaluation)
            => txEvaluation.BeginEvaluations.Concat(txEvaluation.Evaluations).Concat(txEvaluation.EndEvaluations);

        static string GetExceptionName(ActionEvaluation evaluation)
            => GetActualException(evaluation.Exception)?.GetType().Name ?? string.Empty;

        static Exception? GetActualException(Exception? exception) => exception?.InnerException ?? exception;
    }
}
