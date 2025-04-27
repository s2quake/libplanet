using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;

namespace Libplanet.Action.State;

public static class IWorldExtensions
{
    public static FungibleAssetValue GetBalance(
        this IWorldState @this, Address address, Currency currency)
        => @this.GetCurrencyAccount(currency).GetBalance(address, currency);

    public static IWorld MintAsset(
        this IWorld @this, IActionContext context, Address recipient, FungibleAssetValue value)
    {
        if (!value.Currency.CanMint(context.Signer))
        {
            throw new CurrencyPermissionException(
                $"Given {nameof(context)}'s signer {context.Signer} does not have " +
                $"the authority to mint or burn currency {value.Currency}.",
                context.Signer,
                value.Currency);
        }

        return @this.SetCurrencyAccount(@this.GetCurrencyAccount(value.Currency).MintAsset(recipient, value));
    }

    public static IWorld BurnAsset(
        this IWorld @this, IActionContext context, Address owner, FungibleAssetValue value)
    {
        if (!value.Currency.CanMint(context.Signer))
        {
            throw new CurrencyPermissionException(
                $"Given {nameof(context)}'s signer {context.Signer} does not have " +
                $"the authority to mint or burn currency {value.Currency}.",
                context.Signer,
                value.Currency);
        }

        return @this.SetCurrencyAccount(@this.GetCurrencyAccount(value.Currency).BurnAsset(owner, value));
    }

    public static IWorld TransferAsset(
        this IWorld @this, IActionContext context, Address sender, Address recipient, FungibleAssetValue value)
    {
        if (context.BlockProtocolVersion > 0)
        {
            return @this.SetCurrencyAccount(
                @this.GetCurrencyAccount(value.Currency).TransferAsset(sender, recipient, value));
        }

        return @this.SetCurrencyAccount(
            @this.GetCurrencyAccount(value.Currency).TransferAssetV0(sender, recipient, value));
    }

    public static FungibleAssetValue GetTotalSupply(this IWorldState @this, Currency currency)
        => @this.GetCurrencyAccount(currency).GetTotalSupply(currency);

    public static ImmutableSortedSet<Validator> GetValidatorSet(this IWorldState @this)
        => @this.GetValidatorSetAccount().GetValidatorSet();

    public static IWorld SetValidatorSet(this IWorld @this, ImmutableSortedSet<Validator> validatorSet)
        => @this.SetValidatorSetAccount(
            @this.GetValidatorSetAccount().SetValidatorSet(validatorSet));

    internal static ValidatorSetAccount GetValidatorSetAccount(this IWorldState @this)
        => new(
            @this.GetAccountState(ReservedAddresses.ValidatorSetAccount).Trie,
            @this.Version);

    internal static CurrencyAccount GetCurrencyAccount(
        this IWorldState @this, Currency currency)
        => new(
                @this.GetAccountState(new Address(currency.Hash.Bytes)).Trie,
                @this.Version,
                currency);

    internal static IWorld SetCurrencyAccount(
        this IWorld @this, CurrencyAccount currencyAccount)
    {
        if (@this.Version != currencyAccount.WorldVersion)
        {
            throw new ArgumentException(
                $"Given {nameof(currencyAccount)} must have the same version as " +
                $"the version of the world {@this.Version}: " +
                $"{currencyAccount.WorldVersion}",
                nameof(currencyAccount));
        }

        return @this.SetAccount(
            new Address(currencyAccount.Currency.Hash.Bytes),
            currencyAccount.AsAccount());
    }

    internal static IWorld SetValidatorSetAccount(
        this IWorld @this, ValidatorSetAccount validatorSetAccount)
    {
        if (@this.Version != validatorSetAccount.WorldVersion)
        {
            throw new ArgumentException(
                $"Given {nameof(validatorSetAccount)} must have the same version as " +
                $"the version of the world {@this.Version}: " +
                $"{validatorSetAccount.WorldVersion}",
                nameof(validatorSetAccount));
        }

        return @this.SetAccount(
            ReservedAddresses.ValidatorSetAccount,
            validatorSetAccount.AsAccount());
    }
}
