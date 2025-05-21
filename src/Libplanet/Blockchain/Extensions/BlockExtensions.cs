using Libplanet.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain.Extensions;

public static class BlockExtensions
{
    public static void Validate(this Block @this, BlockChain blockChain)
    {
        var items = new Dictionary<object, object?>
        {
            { typeof(BlockChain), blockChain }
        };

        ValidationUtility.Validate(@this, items);
    }
}
