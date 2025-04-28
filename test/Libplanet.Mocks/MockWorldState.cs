using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Mocks;

public sealed class MockWorldState : IWorldState
{
    private readonly IStateStore _stateStore;

    internal MockWorldState(ITrie trie, IStateStore stateStore)
    {
        Trie = trie;
        _stateStore = stateStore;
        Version = trie.GetMetadata() is { } value
            ? value.Version
            : 0;
    }

    public ITrie Trie { get; }

    public bool Legacy => false;

    public int Version { get; }

    public static MockWorldState CreateLegacy(IStateStore? stateStore = null)
    {
        stateStore ??= new TrieStateStore(new MemoryKeyValueStore());
        return new MockWorldState(stateStore.GetStateRoot(default), stateStore);
    }

    public static MockWorldState CreateModern(
        IStateStore? stateStore = null,
        int version = BlockMetadata.CurrentProtocolVersion)
    {
        stateStore ??= new TrieStateStore(new MemoryKeyValueStore());
        ITrie trie = stateStore.GetStateRoot(default);
        trie = trie.SetMetadata(new TrieMetadata(version));
        trie = stateStore.Commit(trie);
        return new MockWorldState(trie, stateStore);
    }

    public IAccountState GetAccountState(Address address) =>
        Legacy && address.Equals(ReservedAddresses.LegacyAccount)
            ? new AccountState(Trie)
            : new AccountState(
                Trie[ToStateKey(address)] is { } stateRootNotNull
                    ? _stateStore.GetStateRoot(ModelSerializer.Deserialize<HashDigest<SHA256>>(stateRootNotNull))
                    : _stateStore.GetStateRoot(default));

    public MockWorldState SetAccount(Address address, IAccountState accountState)
    {
        if (Legacy)
        {
            if (!address.Equals(ReservedAddresses.LegacyAccount))
            {
                throw new ArgumentException(
                    $"Cannot set an account to a non legacy address {address} for " +
                    $"a legacy world");
            }

            ITrie trie = _stateStore.Commit(accountState.Trie);
            return new MockWorldState(trie, _stateStore);
        }
        else
        {
            ITrie trie = _stateStore.Commit(accountState.Trie);
            trie = Trie.Set(ToStateKey(address), new Binary(trie.Hash.Bytes));
            trie = _stateStore.Commit(trie);
            return new MockWorldState(trie, _stateStore);
        }
    }

    public MockWorldState SetBalance(Address address, FungibleAssetValue value) =>
        SetBalance(address, value.Currency, new Integer(value.RawValue));

    public MockWorldState SetBalance(Address address, Currency currency, Integer rawValue)
    {
        // if (Version >= BlockMetadata.CurrencyAccountProtocolVersion)
        {
            Address accountAddress = new Address(currency.Hash.Bytes);
            KeyBytes balanceKey = ToStateKey(address);
            KeyBytes totalSupplyKey = ToStateKey(CurrencyAccount.TotalSupplyAddress);

            ITrie trie = GetAccountState(accountAddress).Trie;
            Integer balance = trie[balanceKey] is Integer b
                ? b
                : new Integer(0);
            Integer totalSupply = trie[totalSupplyKey] is Integer t
                ? t
                : new Integer(0);

            trie = trie.Set(
                totalSupplyKey,
                new Integer(totalSupply.Value - balance.Value + rawValue.Value));
            trie = trie.Set(balanceKey, rawValue);
            return SetAccount(accountAddress, new Account(new AccountState(trie)));
        }
        // else
        // {
        //     Address accountAddress = ReservedAddresses.LegacyAccount;
        //     KeyBytes balanceKey = ToFungibleAssetKey(address, currency);
        //     KeyBytes totalSupplyKey = ToTotalSupplyKey(currency);

        //     ITrie trie = GetAccountState(accountAddress).Trie;
        //     Integer balance = trie[balanceKey] is Integer b
        //         ? b
        //         : new Integer(0);
        //     Integer totalSupply = trie[totalSupplyKey] is Integer t
        //         ? t
        //         : new Integer(0);
        //     trie = trie.Set(
        //         totalSupplyKey,
        //         new Integer(totalSupply.Value - balance.Value + rawValue.Value));

        //     trie = trie.Set(balanceKey, rawValue);
        //     return SetAccount(accountAddress, new AccountState(trie));
        // }
    }

    public MockWorldState SetValidatorSet(ImmutableSortedSet<Validator> validatorSet)
    {
        // var validatorSetAccount = this.GetValidatorSetAccount();
        // var value = ModelSerializer.Serialize(validatorSet);
        // validatorSetAccount = validatorSetAccount.SetValidatorSet(validatorSet);
        // return SetAccount(ReservedAddresses.ValidatorSetAccount, validatorSetAccount.AsAccount());
        throw new NotImplementedException();
    }
}
