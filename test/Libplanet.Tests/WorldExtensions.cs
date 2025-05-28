using Libplanet.State;
using Libplanet.Types;

namespace Libplanet.Tests;

public static class WorldExtensions
{
    public static World SetBalance(this World @this, Address address, FungibleAssetValue value)
    {
        var currencyAccount = @this.GetCurrencyAccount(value.Currency);
        var rawValue = value.RawValue;
        var balance = currencyAccount.GetBalance(address).RawValue;
        var totalSupply = currencyAccount.GetTotalSupply().RawValue;

        currencyAccount = WriteRawBalance(currencyAccount, address, rawValue);
        currencyAccount = WriteRawTotalSupply(currencyAccount, totalSupply - balance + rawValue);
        return @this.SetCurrencyAccount(currencyAccount);
    }

    public static World SetBalance(this World @this, Address address, Currency currency, decimal value)
        => SetBalance(@this, address, currency * value);

    private static CurrencyAccount WriteRawBalance(
        CurrencyAccount currencyAccount, Address address, BigInteger rawValue)
    {
        var trie = currencyAccount.Trie.Set(address.ToString(), rawValue);
        return currencyAccount with { Trie = trie };
    }

    private static CurrencyAccount WriteRawTotalSupply(CurrencyAccount currencyAccount, BigInteger rawValue)
    {
        var key = CurrencyAccount.TotalSupplyAddress.ToString();
        var value = rawValue;
        var trie = currencyAccount.Trie.Set(key, value);
        return currencyAccount with { Trie = trie };
    }
}
