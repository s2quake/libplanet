using System.Diagnostics.CodeAnalysis;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Libplanet.Action;

public interface IWorldContext
{
    bool IsReadOnly { get; }

    IAccountContext this[Address address] { get; }

    object this[Address address, Address stateAddress]
    {
        get => this[address][stateAddress];
        set => this[address][stateAddress] = value;
    }

    FungibleAssetValue GetBalance(Address address, Currency currency);

    void MintAsset(FungibleAssetValue value);

    void TransferAsset(Address sender, Address recipient, FungibleAssetValue value);

    bool TryGetValue<T>(Address address, Address stateAddress, [MaybeNullWhen(false)] out T value)
    {
        if (this[address].TryGetValue<T>(stateAddress, out var obj))
        {
            value = obj;
            return true;
        }

        value = default;
        return false;
    }

    T GetValue<T>(Address address, Address stateAddress, T fallback)
        => this[address].GetValue(stateAddress, fallback);

    bool Contains(Address address, Address stateAddress)
        => this[address].Contains(stateAddress);

    bool Remove(Address address, Address stateAddress)
        => this[address].Remove(stateAddress);
}
