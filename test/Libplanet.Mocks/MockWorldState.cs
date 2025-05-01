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

public sealed class MockWorldState : IWorld
{
    private readonly IStateStore _stateStore;

    internal MockWorldState(ITrie trie, IStateStore stateStore, Address signer)
    {
        Trie = trie;
        _stateStore = stateStore;
        Version = trie.GetMetadata() is { } value ? value.Version : 0;
        Signer = signer;
    }

    public ITrie Trie { get; }

    public Address Signer { get; }

    public int Version { get; }

    public ImmutableDictionary<Address, IAccount> Delta => throw new NotImplementedException();

    public static MockWorldState CreateLegacy(Address signer, IStateStore? stateStore = null)
    {
        stateStore ??= new TrieStateStore();
        return new MockWorldState(stateStore.GetStateRoot(default), stateStore, signer);
    }

    public static MockWorldState CreateModern(
        Address signer,
        IStateStore? stateStore = null,
        int version = BlockMetadata.CurrentProtocolVersion)
    {
        stateStore ??= new TrieStateStore();
        ITrie trie = stateStore.GetStateRoot(default);
        trie = trie.SetMetadata(new TrieMetadata(version));
        trie = stateStore.Commit(trie);
        return new MockWorldState(trie, stateStore, signer);
    }

    public IAccount GetAccount(Address address)
    {
        if (Trie.TryGetValue(ToStateKey(address), out var value))
        {
            return new Account(
            _stateStore.GetStateRoot(ModelSerializer.Deserialize<HashDigest<SHA256>>(value)));
        }
        else
        {
            return new Account(new Trie());
        }
    }

    public MockWorldState SetAccount(Address address, IAccount accountState)
    {
        ITrie trie = _stateStore.Commit(accountState.Trie);
        trie = Trie.Set(ToStateKey(address), new Binary(trie.Hash.Bytes));
        trie = _stateStore.Commit(trie);
        return new MockWorldState(trie, _stateStore);
    }

    public MockWorldState SetBalance(Address address, FungibleAssetValue value) =>
        SetBalance(address, value.Currency, new Integer(value.RawValue));

    public MockWorldState SetBalance(Address address, Currency currency, Integer rawValue)
    {
        Address accountAddress = new Address(currency.Hash.Bytes);
        KeyBytes balanceKey = ToStateKey(address);
        KeyBytes totalSupplyKey = ToStateKey(CurrencyAccount.TotalSupplyAddress);

        ITrie trie = GetAccount(accountAddress).Trie;
        Integer balance = trie.GetValue(balanceKey, new Integer(0));
        Integer totalSupply = trie.GetValue(totalSupplyKey, new Integer(0));

        trie = trie.Set(
            totalSupplyKey,
            new Integer(totalSupply.Value - balance.Value + rawValue.Value));
        trie = trie.Set(balanceKey, rawValue);
        return SetAccount(accountAddress, new Account(trie));

    }

    public MockWorldState SetValidatorSet(ImmutableSortedSet<Validator> validatorSet)
    {
        // var validatorSetAccount = this.GetValidatorSetAccount();
        // var value = ModelSerializer.Serialize(validatorSet);
        // validatorSetAccount = validatorSetAccount.SetValidatorSet(validatorSet);
        // return SetAccount(ReservedAddresses.ValidatorSetAccount, validatorSetAccount.AsAccount());
        throw new NotImplementedException();
    }

    IWorld IWorld.SetAccount(Address address, IAccount account)
    {
        return SetAccount(address, account);
    }
}
