using System.Diagnostics;
using Libplanet.Action;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    // internal void RenderActions(IReadOnlyList<CommittedActionEvaluation> evaluations, Block block)
    // {
    //     foreach (var evaluation in evaluations)
    //     {
    //         _renderAction.OnNext(RenderActionInfo.Create(evaluation));
    //     }
    // }

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
            path.Add(chain.Store.GetBlockHash(chain.Id, idx));
        }

        return path;
    }
}
