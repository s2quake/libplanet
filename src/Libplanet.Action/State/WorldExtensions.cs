using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;
using Libplanet.Types.Crypto;
using static Libplanet.Action.State.ReservedAddresses;

namespace Libplanet.Action.State;

public static class WorldExtensions
{
    public static FungibleAssetValue GetBalance(this World @this, Address address, Currency currency)
        => @this.GetCurrencyAccount(currency).GetBalance(address);

    public static World MintAsset(this World @this, Address recipient, FungibleAssetValue value)
    {
        var currencyAccount = @this.GetCurrencyAccount(value.Currency);
        currencyAccount = currencyAccount.MintAsset(recipient, value.RawValue);
        return @this.SetCurrencyAccount(currencyAccount);
    }

    public static World BurnAsset(this World @this, Address owner, FungibleAssetValue value)
    {
        var currencyAccount = @this.GetCurrencyAccount(value.Currency);
        currencyAccount = currencyAccount.BurnAsset(owner, value.RawValue);
        return @this.SetCurrencyAccount(currencyAccount);
    }

    public static World TransferAsset(this World @this, Address sender, Address recipient, FungibleAssetValue value)
    {
        var currencyAccount = @this.GetCurrencyAccount(value.Currency);
        currencyAccount = currencyAccount.TransferAsset(sender, recipient, value.RawValue);
        return @this.SetCurrencyAccount(currencyAccount);
    }

    public static FungibleAssetValue GetTotalSupply(this World @this, Currency currency)
        => @this.GetCurrencyAccount(currency).GetTotalSupply();

    public static ImmutableSortedSet<Validator> GetValidatorSet(this World @this)
    {
        var account = @this.GetAccount(ValidatorSetAddress);
        return (ImmutableSortedSet<Validator>)account.GetValue(ValidatorSetAddress);
    }

    public static World SetValidatorSet(this World @this, ImmutableSortedSet<Validator> validators)
    {
        var account = @this.GetAccount(ValidatorSetAddress);
        account = account.SetValue(ValidatorSetAddress, validators);
        return @this.SetAccount(ValidatorSetAddress, account);
    }

    public static object? GetValueOrDefault(this World @this, string name, string key)
        => @this.GetAccount(name).GetValueOrDefault(key);

    public static T GetValueOrFallback<T>(this World @this, string name, string key, T fallback)
        => @this.GetAccount(name).GetValueOrFallback(key, fallback);

    public static object GetValue(this World @this, string name, string key)
        => @this.GetAccount(name).GetValue(key);

    public static World SetValue(this World @this, string name, string key, object value)
        => @this.SetAccount(name, @this.GetAccount(name).SetValue(key, value));

    internal static CurrencyAccount GetCurrencyAccount(this World @this, Currency currency)
        => new(@this.GetAccount(new KeyBytes(currency.Hash.Bytes)).Trie, @this.Signer, currency);

    internal static World SetCurrencyAccount(this World @this, CurrencyAccount currencyAccount)
        => @this.SetAccount(new KeyBytes(currencyAccount.Currency.Hash.Bytes), currencyAccount.AsAccount());
}
