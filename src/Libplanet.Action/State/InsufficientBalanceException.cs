using Libplanet.Types.Crypto;
using Libplanet.Types.Assets;

namespace Libplanet.Action.State;

public sealed class InsufficientBalanceException(string message, Address address, FungibleAssetValue balance)
    : Exception(message)
{
    public Address Address { get; } = address;

    public FungibleAssetValue Balance { get; } = balance;
}
