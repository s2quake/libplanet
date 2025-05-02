using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;

namespace Libplanet.Action.State;

public sealed record class CurrencyAccount(ITrie Trie, int WorldVersion, Currency Currency)
{
    public static readonly Address TotalSupplyAddress = Address.Parse("1000000000000000000000000000000000000000");

    public FungibleAssetValue GetBalance(Address address, Currency currency)
    {
        CheckCurrency(currency);
        return new FungibleAssetValue
        {
            Currency = Currency,
            RawValue = GetRawBalance(address),
        };
    }

    public FungibleAssetValue GetTotalSupply(Currency currency)
    {
        CheckCurrency(currency);
        return new FungibleAssetValue
        {
            Currency = Currency,
            RawValue = GetRawTotalSupply(),
        };
    }

    public CurrencyAccount MintAsset(Address recipient, FungibleAssetValue value)
    {
        CheckCurrency(value.Currency);
        if (value.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"The amount to mint, burn, or transfer must be greater than zero: {value}");
        }

        return MintRawAsset(recipient, value.RawValue);
    }

    public CurrencyAccount BurnAsset(Address owner, FungibleAssetValue value)
    {
        CheckCurrency(value.Currency);
        if (value.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"The amount to mint, burn, or transfer must be greater than zero: {value}");
        }

        return BurnRawAsset(owner, value.RawValue);
    }

    public CurrencyAccount TransferAsset(Address sender, Address recipient, FungibleAssetValue value)
    {
        CheckCurrency(value.Currency);
        if (value.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"The amount to mint, burn, or transfer must be greater than zero: {value}");
        }

        return TransferRawAsset(sender, recipient, value.RawValue);
    }

    public Account AsAccount() => new Account(Trie);

    private CurrencyAccount MintRawAsset(Address recipient, BigInteger rawValue)
    {
        var currencyAccount = this;
        var prevBalanceRawValue = currencyAccount.GetRawBalance(recipient);
        currencyAccount = currencyAccount.WriteRawBalance(recipient, prevBalanceRawValue + rawValue);

        var prevTotalSupplyRawValue = currencyAccount.GetRawTotalSupply();
        if (Currency.MaximumSupply != BigInteger.Zero && Currency.MaximumSupply < prevTotalSupplyRawValue + rawValue)
        {
            var prevTotalSupply = new FungibleAssetValue { Currency = Currency, RawValue = prevTotalSupplyRawValue };
            var value = new FungibleAssetValue { Currency = Currency, RawValue = rawValue };
            throw new SupplyOverflowException(
                $"Cannot mint {value} in addition to " +
                $"the current total supply of {prevTotalSupply} as it would exceed " +
                $"the maximum supply {Currency.MaximumSupply}.",
                prevTotalSupply);
        }

        currencyAccount = currencyAccount.WriteRawTotalSupply(prevTotalSupplyRawValue + rawValue);

        return currencyAccount;
    }

    private CurrencyAccount BurnRawAsset(Address owner, BigInteger rawValue)
    {
        var currencyAccount = this;
        var prevBalanceRawValue = currencyAccount.GetRawBalance(owner);
        if (prevBalanceRawValue - rawValue < 0)
        {
            var prevBalance = new FungibleAssetValue { Currency = Currency, RawValue = prevBalanceRawValue };
            var value = new FungibleAssetValue { Currency = Currency, RawValue = rawValue };
            throw new InsufficientBalanceException(
                $"Cannot burn or transfer {value} from {owner} as the current balance " +
                $"of {owner} is {prevBalance}.",
                owner,
                prevBalance);
        }

        currencyAccount = currencyAccount.WriteRawBalance(owner, prevBalanceRawValue - rawValue);

        var prevTotalSupplyRawValue = currencyAccount.GetRawTotalSupply();
        currencyAccount = currencyAccount.WriteRawTotalSupply(prevTotalSupplyRawValue - rawValue);

        return currencyAccount;
    }

    private CurrencyAccount TransferRawAsset(Address sender, Address recipient, BigInteger rawValue)
    {
        var currencyAccount = this;
        var prevSenderBalanceRawValue = currencyAccount.GetRawBalance(sender);
        if (prevSenderBalanceRawValue - rawValue < 0)
        {
            var prevSenderBalance = new FungibleAssetValue
            {
                Currency = Currency,
                RawValue = prevSenderBalanceRawValue,
            };
            var value = new FungibleAssetValue { Currency = Currency, RawValue = rawValue };
            throw new InsufficientBalanceException(
                $"Cannot burn or transfer {value} from {sender} as the current balance " +
                $"of {sender} is {prevSenderBalance}.",
                sender,
                prevSenderBalance);
        }

        currencyAccount = currencyAccount.WriteRawBalance(sender, prevSenderBalanceRawValue - rawValue);

        var prevRecipientBalanceRawValue = currencyAccount.GetRawBalance(recipient);
        currencyAccount = currencyAccount.WriteRawBalance(recipient, prevRecipientBalanceRawValue + rawValue);
        return currencyAccount;
    }

    private CurrencyAccount WriteRawBalance(Address address, BigInteger rawValue) => this with
    {
        Trie = Trie.Set(KeyConverters.ToStateKey(address), new Integer(rawValue)),
    };

    private CurrencyAccount WriteRawTotalSupply(BigInteger rawValue) => this with
    {
        Trie = Trie.Set(KeyConverters.ToStateKey(TotalSupplyAddress), new Integer(rawValue)),
    };

    private BigInteger GetRawBalance(Address address)
        => Trie.GetValue(KeyConverters.ToStateKey(address), (Integer)0).Value;

    private BigInteger GetRawTotalSupply()
        => Trie.GetValue(KeyConverters.ToStateKey(TotalSupplyAddress), (Integer)0).Value;

    private void CheckCurrency(Currency currency)
    {
        if (!Currency.Equals(currency))
        {
            throw new ArgumentException(
                $"Given currency {currency} should match the account's currency {Currency}.",
                nameof(currency));
        }
    }
}
