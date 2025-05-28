using Libplanet.Types;

namespace Libplanet.State;

public sealed class SupplyOverflowException(string message, FungibleAssetValue amount)
    : Exception(message)
{
    public FungibleAssetValue Amount { get; } = amount;
}
