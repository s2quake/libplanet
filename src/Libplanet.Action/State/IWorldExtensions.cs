using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;

namespace Libplanet.Action.State;

public static class IWorldExtensions
{
    public static FungibleAssetValue GetBalance(
        this IWorldState worldState,
        Address address,
        Currency currency) =>
            worldState.GetCurrencyAccount(currency).GetBalance(address, currency);

    public static IWorld MintAsset(
        this IWorld world,
        IActionContext context,
        Address recipient,
        FungibleAssetValue value) => value.Currency.CanMint(context.Signer)
            ? world.SetCurrencyAccount(
                world.GetCurrencyAccount(value.Currency).MintAsset(recipient, value))
            : throw new CurrencyPermissionException(
                $"Given {nameof(context)}'s signer {context.Signer} does not have " +
                $"the authority to mint or burn currency {value.Currency}.",
                context.Signer,
                value.Currency);

    public static IWorld BurnAsset(
        this IWorld world,
        IActionContext context,
        Address owner,
        FungibleAssetValue value) => value.Currency.CanMint(context.Signer)
            ? world.SetCurrencyAccount(
                world.GetCurrencyAccount(value.Currency).BurnAsset(owner, value))
            : throw new CurrencyPermissionException(
                $"Given {nameof(context)}'s signer {context.Signer} does not have " +
                $"the authority to mint or burn currency {value.Currency}.",
                context.Signer,
                value.Currency);

    public static IWorld TransferAsset(
        this IWorld world,
        IActionContext context,
        Address sender,
        Address recipient,
        FungibleAssetValue value) =>
            context.BlockProtocolVersion > 0
                ? world.SetCurrencyAccount(
                    world
                        .GetCurrencyAccount(value.Currency)
                        .TransferAsset(sender, recipient, value))
#pragma warning disable CS0618 // Obsolete
                : world.SetCurrencyAccount(
                    world
                        .GetCurrencyAccount(value.Currency)
                        .TransferAssetV0(sender, recipient, value));
#pragma warning restore CS0618

    public static FungibleAssetValue GetTotalSupply(
        this IWorldState worldState,
        Currency currency) =>
            worldState.GetCurrencyAccount(currency).GetTotalSupply(currency);

    public static ImmutableSortedSet<Validator> GetValidatorSet(this IWorldState worldState) =>
        worldState.GetValidatorSetAccount().GetValidatorSet();

    public static IWorld SetValidatorSet(this IWorld world, ImmutableSortedSet<Validator> validatorSet) =>
        world.SetValidatorSetAccount(
            world.GetValidatorSetAccount().SetValidatorSet(validatorSet));

    internal static ValidatorSetAccount GetValidatorSetAccount(this IWorldState worldState)
        => new ValidatorSetAccount(
            worldState.GetAccountState(ReservedAddresses.ValidatorSetAccount).Trie,
            worldState.Version);

    internal static CurrencyAccount GetCurrencyAccount(
        this IWorldState worldState,
        Currency currency) =>
        new CurrencyAccount(
                worldState.GetAccountState(new Address(currency.Hash.Bytes)).Trie,
                worldState.Version,
                currency);

    internal static IWorld SetCurrencyAccount(
        this IWorld world,
        CurrencyAccount currencyAccount) =>
            world.Version == currencyAccount.WorldVersion
                ? world.SetAccount(
                        new Address(currencyAccount.Currency.Hash.Bytes),
                        currencyAccount.AsAccount())
                : throw new ArgumentException(
                    $"Given {nameof(currencyAccount)} must have the same version as " +
                    $"the version of the world {world.Version}: " +
                    $"{currencyAccount.WorldVersion}",
                    nameof(currencyAccount));

    internal static IWorld SetValidatorSetAccount(
        this IWorld world,
        ValidatorSetAccount validatorSetAccount) =>
            world.Version == validatorSetAccount.WorldVersion
                ? world.SetAccount(
                        ReservedAddresses.ValidatorSetAccount,
                        validatorSetAccount.AsAccount())
                : throw new ArgumentException(
                    $"Given {nameof(validatorSetAccount)} must have the same version as " +
                    $"the version of the world {world.Version}: " +
                    $"{validatorSetAccount.WorldVersion}",
                    nameof(validatorSetAccount));
}
