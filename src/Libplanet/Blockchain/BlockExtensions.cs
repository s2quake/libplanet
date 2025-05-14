using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain;

public static class BlockExtensions
{
    internal static void ValidateAsGenesis(this Block block)
    {
        if (block.Height != 0)
        {
            throw new InvalidOperationException(
                $"Given {nameof(block)} must have index 0 but has index {block.Height}");
        }

        if (block.Version > BlockHeader.CurrentProtocolVersion)
        {
            throw new InvalidOperationException(
                $"The protocol version ({block.Version}) of the block " +
                $"#{block.Height} {block.BlockHash} is not supported by this node." +
                $"The highest supported protocol version is {BlockHeader.CurrentProtocolVersion}.");
        }

        if (block.PreviousHash != default)
        {
            throw new InvalidOperationException(
                "A genesis block should not have previous hash, " +
                $"but its value is {block.PreviousHash}.");
        }

        if (block.LastCommit != BlockCommit.Empty)
        {
            throw new InvalidOperationException(
                "A genesis block should not have last commit, " +
                $"but its value is {block.LastCommit}.");
        }
    }
}
