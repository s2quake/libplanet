using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Libplanet.Tests;

public static class WorldExtensions
{
    public static World SetBalance(this World @this, Address address, FungibleAssetValue value)
        => SetBalance(@this, address, value.Currency, value.RawValue);

    public static World SetBalance(this World @this, Address address, Currency currency, BigInteger rawValue)
    {
        var accountAddress = new Address(currency.Hash.Bytes);
        var account = @this.GetAccount(accountAddress);
        var balance = account.GetValueOrFallback(address, BigInteger.Zero);
        var totalSupply = account.GetValueOrFallback(CurrencyAccount.TotalSupplyAddress, BigInteger.Zero);

        account = account.SetValue(CurrencyAccount.TotalSupplyAddress, totalSupply - balance + rawValue);
        account = account.SetValue(address, rawValue);
        return @this.SetAccount(accountAddress, account);
    }
}
