using Libplanet.Types;

namespace Libplanet.State;

public sealed class InsufficientBalanceException(string message, Address address, FungibleAssetValue balance)
    : Exception(message)
{
    public Address Address { get; } = address;

    public FungibleAssetValue Balance { get; } = balance;
}
