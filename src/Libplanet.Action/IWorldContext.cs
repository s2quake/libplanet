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

    object this[(Address Address, Address StateAddress) key]
    {
        get => this[key.Address][key.StateAddress];
        set => this[key.Address][key.StateAddress] = value;
    }

    FungibleAssetValue GetBalance(Address address, Currency currency);

    void MintAsset(Address recipient, FungibleAssetValue value);

    void BurnAsset(Address owner, FungibleAssetValue value);

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

    bool TryGetValue<T>((Address Address, Address StateAddress) key, [MaybeNullWhen(false)] out T value)
    {
        if (this[key.Address].TryGetValue<T>(key.StateAddress, out var obj))
        {
            value = obj;
            return true;
        }

        value = default;
        return false;
    }

    T GetValue<T>(Address address, Address stateAddress, T fallback)
    {
        if (this[address].TryGetValue<T>(stateAddress, out var obj))
        {
            return obj;
        }

        return fallback;
    }

    T GetValue<T>((Address Address, Address StateAddress) key, T fallback)
        => GetValue(key.Address, key.StateAddress, fallback);

    bool Contains(Address address, Address stateAddress) => this[address].Contains(stateAddress);

    bool Contains((Address Address, Address StateAddress) key) => Contains(key.Address, key.StateAddress);

    bool Remove(Address address, Address stateAddress) => this[address].Remove(stateAddress);

    bool Remove((Address Address, Address StateAddress) key) => Remove(key.Address, key.StateAddress);
}
