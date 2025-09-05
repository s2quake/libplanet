using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Extensions;

public static class BlockExtensions
{
    public static void Validate(this Block @this, Blockchain blockChain)
    {
        var items = new Dictionary<object, object?>
        {
            { typeof(Blockchain), blockChain }
        };

        ModelValidationUtility.Validate(@this, items);
    }

    public static long GetActionByteLength(this Block @this)
        => @this.Transactions.SelectMany(tx => tx.Actions).Aggregate(0L, (s, i) => s + i.Bytes.Length);

    internal static void ValidateAsGenesis(this Block @this)
    {
        if (@this.Height != 0)
        {
            throw new InvalidOperationException(
                $"Given {nameof(@this)} must have index 0 but has index {@this.Height}");
        }

        if (@this.Version > BlockHeader.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"The protocol version ({@this.Version}) of the block " +
                $"#{@this.Height} {@this.BlockHash} is not supported by this node." +
                $"The highest supported protocol version is {BlockHeader.CurrentVersion}.");
        }

        if (@this.PreviousBlockHash != default)
        {
            throw new InvalidOperationException(
                "A genesis block should not have previous hash, " +
                $"but its value is {@this.PreviousBlockHash}.");
        }

        if (@this.PreviousBlockCommit != default)
        {
            throw new InvalidOperationException(
                "A genesis block should not have last commit, " +
                $"but its value is {@this.PreviousBlockCommit}.");
        }
    }
}
