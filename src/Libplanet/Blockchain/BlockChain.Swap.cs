using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    private static IReadOnlyList<BlockHash> GetRewindPath(BlockChain chain, BlockHash targetHash)
    {
        if (!chain.Blocks.ContainsKey(targetHash))
        {
            throw new KeyNotFoundException(
                $"Given chain {chain.Id} must contain target hash {targetHash}");
        }

        var target = chain.Blocks[targetHash].Height;
        var pathList = new List<BlockHash>();
        for (int idx = chain.Tip.Height; idx > target; idx--)
        {
            pathList.Add(chain._chain.BlockHashes[idx]);
        }

        return pathList;
    }
}
