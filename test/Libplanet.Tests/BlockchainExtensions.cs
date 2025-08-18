using Libplanet.Types;
using Libplanet.TestUtilities;

namespace Libplanet.Tests;

public static class BlockchainExtensions
{
    // public static async Task WaitUntilHeightAsync(this Libplanet.Blockchain @this, int height, CancellationToken cancellationToken)
    // {
    //     using var resetEvent = new ManualResetEvent(false);
    //     using var _ = @this.TipChanged.Subscribe(e =>
    //     {
    //         if (e.Tip.Height == height)
    //         {
    //             resetEvent.Set();
    //         }
    //     });

    //     while (@this.Tip.Height < height && !resetEvent.WaitOne(0))
    //     {
    //         await Task.Delay(100, cancellationToken);
    //     }
    // }

    public static (Block, BlockCommit) ProposeAndAppend(this Libplanet.Blockchain @this, PrivateKey signer)
        => ProposeAndAppend(@this, signer.AsSigner());

    public static (Block, BlockCommit) ProposeAndAppend(this Libplanet.Blockchain @this, ISigner signer)
    {
        var block = @this.ProposeBlock(signer);
        var blockCommit = TestUtils.CreateBlockCommit(block);
        @this.Append(block, blockCommit);
        return (block, blockCommit);
    }

    public static (Block, BlockCommit)[] ProposeAndAppendMany(
        this Libplanet.Blockchain @this, PrivateKey signer, int count)
    {
        var blocks = new (Block, BlockCommit)[count];
        for (var i = 0; i < count; i++)
        {
            blocks[i] = @this.ProposeAndAppend(signer);
        }

        return blocks;
    }

    public static (Block, BlockCommit)[] ProposeAndAppendMany(this Libplanet.Blockchain @this, int count)
        => ProposeAndAppendMany(@this, Random.Shared, count);

    public static (Block, BlockCommit)[] ProposeAndAppendMany(
        this Libplanet.Blockchain @this, Random random, int count)
    {
        var blocks = new (Block, BlockCommit)[count];
        for (var i = 0; i < count; i++)
        {
            var signer = RandomUtility.PrivateKey(random);
            blocks[i] = @this.ProposeAndAppend(signer);
        }

        return blocks;
    }

    public static BlockCommit AppendWithBlockCommit(this Libplanet.Blockchain @this, Block block)
    {
        var blockCommit = TestUtils.CreateBlockCommit(block);
        @this.Append(block, blockCommit);
        return blockCommit;
    }

    public static void AppendTo(this Libplanet.Blockchain @this, Libplanet.Blockchain other, Range range)
    {
        var (start, length) = range.GetOffsetAndLength(@this.Blocks.Count);
        for (var i = 0; i < length; i++)
        {
            var index = start + i;
            var block = @this.Blocks[index];
            var blockCommit = @this.BlockCommits[index];
            other.Append(block, blockCommit);
        }
    }
}
