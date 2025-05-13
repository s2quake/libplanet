using Libplanet.Action;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    internal IEnumerable<TxExecution> MakeTxExecutions(
        Block block,
        IReadOnlyList<CommittedActionEvaluation> evaluations)
    {
        List<(TxId?, List<CommittedActionEvaluation>)> groupedEvals =
            new List<(TxId?, List<CommittedActionEvaluation>)>();
        foreach (CommittedActionEvaluation eval in evaluations)
        {
            if (groupedEvals.Count == 0)
            {
                groupedEvals.Add(
                    (eval.InputContext.TxId, new List<CommittedActionEvaluation>() { eval }));
            }
            else
            {
                if (groupedEvals[^1].Item1.Equals(eval.InputContext.TxId))
                {
                    groupedEvals[^1].Item2.Add(eval);
                }
                else
                {
                    groupedEvals.Add(
                        (
                            eval.InputContext.TxId,
                            new List<CommittedActionEvaluation>() { eval }));
                }
            }
        }

        ITrie trie = GetWorld(block.StateRootHash).Trie;

        int count = 0;
        foreach (var group in groupedEvals)
        {
            if (group.Item1 is { } txId)
            {
                // If txId is not null, group has at least one element.
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
                    TxId = txId,
                    InputState = group.Item2.First().InputContext.PreviousState,
                    OutputState = group.Item2[^1].OutputState,
                    ExceptionNames = [.. exceptions.Select(item => item.Message)],
                };

                count++;
            }
        }

        _logger.Verbose(
            "Prepared " + nameof(TxExecution) +
            "s for {Txs} transactions within the block #{BlockHeight} {BlockHash}",
            count,
            block.Height,
            block.BlockHash);
    }
}
