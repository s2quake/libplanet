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

    internal static void ValidateAsGenesis(this Block @this)
    {
        if (@this.Height != 0)
        {
            throw new InvalidOperationException(
                $"Given {nameof(@this)} must have index 0 but has index {@this.Height}");
        }

        if (@this.Version > BlockHeader.CurrentProtocolVersion)
        {
            throw new InvalidOperationException(
                $"The protocol version ({@this.Version}) of the block " +
                $"#{@this.Height} {@this.BlockHash} is not supported by this node." +
                $"The highest supported protocol version is {BlockHeader.CurrentProtocolVersion}.");
        }

        if (@this.PreviousHash != default)
        {
            throw new InvalidOperationException(
                "A genesis block should not have previous hash, " +
                $"but its value is {@this.PreviousHash}.");
        }

        if (@this.PreviousCommit != BlockCommit.Empty)
        {
            throw new InvalidOperationException(
                "A genesis block should not have last commit, " +
                $"but its value is {@this.PreviousCommit}.");
        }
    }
}
