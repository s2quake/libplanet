using Libplanet.Data.Structures;
using Libplanet.Types;

namespace Libplanet.State;

public sealed record class CurrencyAccount(ITrie Trie, Address Signer, Currency Currency)
{
    public static readonly Address TotalSupplyAddress = Address.Parse("1000000000000000000000000000000000000000");

    public FungibleAssetValue GetBalance(Address address) => new()
    {
        Currency = Currency,
        RawValue = GetRawBalance(address),
    };

    public FungibleAssetValue GetTotalSupply() => new()
    {
        Currency = Currency,
        RawValue = GetRawTotalSupply(),
    };

    public CurrencyAccount MintAsset(Address recipient, BigInteger rawValue) => MintRawAsset(recipient, rawValue);

    public CurrencyAccount MintAsset(Address recipient, decimal value)
        => MintRawAsset(recipient, (Currency * value).RawValue);

    public CurrencyAccount BurnAsset(Address owner, BigInteger rawValue) => BurnRawAsset(owner, rawValue);

    public CurrencyAccount BurnAsset(Address owner, decimal value)
        => BurnRawAsset(owner, (Currency * value).RawValue);

    public CurrencyAccount TransferAsset(Address sender, Address recipient, BigInteger rawValue)
        => TransferRawAsset(sender, recipient, rawValue);

    public CurrencyAccount TransferAsset(Address sender, Address recipient, decimal value)
        => TransferRawAsset(sender, recipient, (Currency * value).RawValue);

    public Account AsAccount() => new(Trie);

    private CurrencyAccount MintRawAsset(Address recipient, BigInteger rawValue)
    {
        if (rawValue.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawValue),
                $"The amount to mint, burn, or transfer must be greater than zero: {rawValue}");
        }

        if (!Currency.CanMint(Signer))
        {
            throw new CurrencyPermissionException(
                $"Given {nameof(CurrencyAccount)}'s recipient {Signer} does not have " +
                $"the authority to mint or burn currency {Currency}.",
                Signer,
                Currency);
        }

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
        if (rawValue.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawValue),
                $"The amount to mint, burn, or transfer must be greater than zero: {rawValue}");
        }

        if (!Currency.CanMint(Signer))
        {
            throw new CurrencyPermissionException(
                $"Given {nameof(CurrencyAccount)}'s owner {Signer} does not have " +
                $"the authority to mint or burn currency {Currency}.",
                Signer,
                Currency);
        }

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
        if (rawValue.Sign <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawValue),
                $"The amount to mint, burn, or transfer must be greater than zero: {rawValue}");
        }

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
        Trie = Trie.Set(address.ToString(), rawValue),
    };

    private CurrencyAccount WriteRawTotalSupply(BigInteger rawValue) => this with
    {
        Trie = Trie.Set(TotalSupplyAddress.ToString(), rawValue),
    };

    private BigInteger GetRawBalance(Address address)
        => Trie.GetValueOrDefault(address.ToString(), BigInteger.Zero);

    private BigInteger GetRawTotalSupply()
        => Trie.GetValueOrDefault(TotalSupplyAddress.ToString(), BigInteger.Zero);
}
