using Libplanet.Action;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain
{
    public partial class BlockChain
    {
        /// <summary>
        /// Makes <see cref="TxExecution"/> instances from the given <paramref name="evaluations"/>.
        /// </summary>
        /// <param name="block">The block that evaluated actions belong to.</param>
        /// <param name="evaluations">The result of evaluated actions.</param>
        /// <returns>The corresponding <see cref="TxExecution"/>s.</returns>
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
                    if (groupedEvals.Last().Item1.Equals(eval.InputContext.TxId))
                    {
                        groupedEvals.Last().Item2.Add(eval);
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
                        BlockHash = block.Hash,
                        TxId = txId,
                        InputState = group.Item2.First().InputContext.PreviousState,
                        OutputState = group.Item2.Last().OutputState,
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
                block.Hash);
        }

        internal void UpdateTxExecutions(IEnumerable<TxExecution> txExecutions)
        {
            int count = 0;
            foreach (TxExecution txExecution in txExecutions)
            {
                Store.PutTxExecution(txExecution);
                count++;

                _logger.Verbose(
                    "Updated " + nameof(TxExecution) + " for tx {TxId} within block {BlockHash}",
                    txExecution.TxId,
                    txExecution.BlockHash);
            }

            _logger.Verbose(
                "Updated " + nameof(TxExecution) + "s for {Txs} transactions",
                count);
        }
    }
}
