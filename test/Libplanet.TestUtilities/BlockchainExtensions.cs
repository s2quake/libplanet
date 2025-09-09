using Libplanet.Types;

namespace Libplanet.TestUtilities;

public static class BlockchainExtensions
{
    public static (Block, BlockCommit) ProposeAndAppend(
        this Blockchain @this, ISigner signer, ImmutableSortedSet<TestValidator> validators)
    {
        var block = @this.Propose(signer);
        var blockCommit = TestUtility.CreateBlockCommit(block, validators);
        @this.Append(block, blockCommit);
        return (block, blockCommit);
    }

    public static (Block Block, BlockCommit BlockCommit)[] ProposeAndAppendMany(
        this Blockchain @this, ISigner signer, ImmutableSortedSet<TestValidator> validators, int count)
    {
        var blocks = new (Block, BlockCommit)[count];
        for (var i = 0; i < count; i++)
        {
            blocks[i] = ProposeAndAppend(@this, signer, validators);
        }

        return blocks;
    }
}
