using System.Diagnostics;
using Libplanet.Action;
using Libplanet.Blockchain.Renderers;
using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain
{
    public partial class BlockChain
    {
        /// <summary>
        /// Render actions of the given <paramref name="block"/>.
        /// </summary>
        /// <param name="evaluations"><see cref="IActionEvaluation"/>s of the block.  If it is
        /// <see langword="null"/>, evaluate actions of the <paramref name="block"/> again.</param>
        /// <param name="block"><see cref="Block"/> to render actions.</param>
        internal void RenderActions(
            IReadOnlyList<ICommittedActionEvaluation> evaluations,
            Block block)
        {
            if (evaluations is null)
            {
                throw new NullReferenceException(nameof(evaluations));
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            _logger.Debug(
                "Rendering actions in block #{BlockHeight} {BlockHash}...",
                block.Height,
                block.Hash);

            long count = 0;
            foreach (var evaluation in evaluations)
            {
                foreach (IActionRenderer renderer in ActionRenderers)
                {
                    if (evaluation.Exception is null)
                    {
                        renderer.RenderAction(
                            evaluation.Action,
                            evaluation.InputContext,
                            evaluation.OutputState);
                    }
                    else
                    {
                        renderer.RenderActionError(
                            evaluation.Action,
                            evaluation.InputContext,
                            evaluation.Exception);
                    }

                    count++;
                }
            }

            _logger
                .ForContext("Tag", "Metric")
                .ForContext("Subtag", "BlockRenderDuration")
                .Debug(
                    "Finished rendering {RenderCount} renders for actions in " +
                    "block #{BlockHeight} {BlockHash} in {DurationMs} ms",
                    count,
                    block.Height,
                    block.Hash,
                    stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Generates a list of <see cref="BlockHash"/>es to traverse starting from
        /// the tip of <paramref name="chain"/> to reach <paramref name="targetHash"/>.
        /// </summary>
        /// <param name="chain">The <see cref="BlockChain"/> to traverse.</param>
        /// <param name="targetHash">The target <see cref="BlockHash"/> to reach.</param>
        /// <returns>
        /// An <see cref="IReadOnlyList{T}"/> of <see cref="BlockHash"/>es to traverse from
        /// the tip of <paramref name="chain"/> to reach <paramref name="targetHash"/> excluding
        /// <paramref name="targetHash"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This is a reverse of <see cref="GetFastForwardPath"/>.
        /// </para>
        /// <para>
        /// As the genesis is always fixed, returned results never include the genesis.
        /// </para>
        /// </remarks>
        private static IReadOnlyList<BlockHash> GetRewindPath(
            BlockChain chain,
            BlockHash targetHash)
        {
            if (!chain.ContainsBlock(targetHash))
            {
                throw new KeyNotFoundException(
                    $"Given chain {chain.Id} must contain target hash {targetHash}");
            }

            long target = chain[targetHash].Height;
            List<BlockHash> path = new List<BlockHash>();
            for (long idx = chain.Tip.Height; idx > target; idx--)
            {
                path.Add(chain.Store.IndexBlockHash(chain.Id, idx) ??
                    throw new NullReferenceException(
                        $"Chain {chain.Id} is missing block #{idx}"));
            }

            return path;
        }
    }
}
