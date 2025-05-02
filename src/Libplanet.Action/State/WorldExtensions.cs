using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;
using static Libplanet.Action.State.ReservedAddresses;

namespace Libplanet.Action.State;

public static class WorldExtensions
{
    public static FungibleAssetValue GetBalance(this World @this, Address address, Currency currency)
        => @this.GetCurrencyAccount(currency).GetBalance(address, currency);

    public static World MintAsset(this World @this, Address recipient, FungibleAssetValue value)
    {
        if (!value.Currency.CanMint(@this.Signer))
        {
            throw new CurrencyPermissionException(
                $"Given {nameof(World)}'s signer {@this.Signer} does not have " +
                $"the authority to mint or burn currency {value.Currency}.",
                @this.Signer,
                value.Currency);
        }

        return @this.SetCurrencyAccount(@this.GetCurrencyAccount(value.Currency).MintAsset(recipient, value));
    }

    public static World BurnAsset(this World @this, Address owner, FungibleAssetValue value)
    {
        if (!value.Currency.CanMint(@this.Signer))
        {
            throw new CurrencyPermissionException(
                $"Given {nameof(World)}'s signer {@this.Signer} does not have " +
                $"the authority to mint or burn currency {value.Currency}.",
                @this.Signer,
                value.Currency);
        }

        return @this.SetCurrencyAccount(@this.GetCurrencyAccount(value.Currency).BurnAsset(owner, value));
    }

    public static World TransferAsset(this World @this, Address sender, Address recipient, FungibleAssetValue value)
    {
        return @this.SetCurrencyAccount(
            @this.GetCurrencyAccount(value.Currency).TransferAsset(sender, recipient, value));
    }

    public static FungibleAssetValue GetTotalSupply(this World @this, Currency currency)
        => @this.GetCurrencyAccount(currency).GetTotalSupply(currency);

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

    internal static CurrencyAccount GetCurrencyAccount(this World @this, Currency currency)
        => new(
                @this.GetAccount(new Address(currency.Hash.Bytes)).Trie,
                @this.Version,
                currency);

    internal static World SetCurrencyAccount(this World @this, CurrencyAccount currencyAccount)
    {
        if (@this.Version != currencyAccount.WorldVersion)
        {
            throw new ArgumentException(
                $"Given {nameof(currencyAccount)} must have the same version as " +
                $"the version of the world {@this.Version}: " +
                $"{currencyAccount.WorldVersion}",
                nameof(currencyAccount));
        }

        return @this.SetAccount(new Address(currencyAccount.Currency.Hash.Bytes), currencyAccount.AsAccount());
    }
}
