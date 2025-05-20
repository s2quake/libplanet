using Libplanet.Action;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    internal IEnumerable<TxExecution> MakeTxExecutions(Block block, ActionEvaluation[] evaluations)
    {
        var groupedEvals = new List<(TxId, List<ActionEvaluation>)>();
        foreach (var evaluation in evaluations)
        {
            if (groupedEvals.Count == 0)
            {
                groupedEvals.Add(
                    (evaluation.InputContext.TxId, new List<ActionEvaluation>() { evaluation }));
            }
            else
            {
                if (groupedEvals[^1].Item1.Equals(evaluation.InputContext.TxId))
                {
                    groupedEvals[^1].Item2.Add(evaluation);
                }
                else
                {
                    groupedEvals.Add(
                        (
                            evaluation.InputContext.TxId,
                            new List<ActionEvaluation>() { evaluation }));
                }
            }
        }

        int count = 0;
        foreach (var group in groupedEvals)
        {
            List<Exception> exceptions = group.Item2
                .Select(eval => eval.Exception)
                .Select(exception => exception is { } e && e.InnerException is { } i
                    ? i
                    : exception)
                .OfType<Exception>()
                .ToList();

            yield return new TxExecution
            {
                BlockHash = block.BlockHash,
                TxId = group.Item1,
                InputState = group.Item2[0].InputWorld.Trie.Hash,
                OutputState = group.Item2[^1].OutputWorld.Trie.Hash,
                ExceptionNames = [.. exceptions.Select(item => item.Message)],
            };

            count++;
        }
    }
}
