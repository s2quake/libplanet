using Libplanet.Types;

namespace Libplanet.Extensions;

public static class BlockCommitExtensions
{
    public static void Validate(this BlockCommit @this, Block block)
    {
        var items = new Dictionary<object, object?>
        {
            { typeof(Block), block }
        };

        ValidationUtility.Validate(@this, items);
    }
}
