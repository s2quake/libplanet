using Libplanet.Serialization;
using Libplanet.Serialization.DataAnnotations;
using Libplanet.Types;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.State.Tests.Actions;

[Model(Version = 1, TypeName = "Tests_DumbAction")]
public sealed record class DumbAction : ActionBase, IEquatable<DumbAction>
{
    public static readonly DumbAction NoOp = Create();

    public static readonly Currency DumbCurrency = Currency.Create("DUMB", 0);

    [Property(0)]
    public (Address At, string Item)? Append { get; private set; }

    [Property(1)]
    public (Address? From, Address? To, BigInteger Amount)? Transfer { get; private set; }

    [NotDefault]
    [Property(2)]
    public ImmutableSortedSet<Validator>? Validators { get; private set; }

    [Property(3)]
    [NotDefault]
    public Address AccountAddress { get; init; } = SystemAccount;

    public static DumbAction Create(
        (Address At, string Item)? append = null,
        (Address? From, Address? To, BigInteger Amount)? transfer = null,
        ImmutableSortedSet<Validator>? validators = null)
    {
        if (transfer is { } t && t.From is null && t.To is null)
        {
            throw new ArgumentException(
                $"From and To of {nameof(transfer)} cannot both be null when " +
                $"{nameof(transfer)} is not null: {transfer}");
        }

        return new DumbAction()
        {
            Append = append,
            Transfer = transfer,
            Validators = validators,
        };
    }

    public override int GetHashCode() => ModelResolver.GetHashCode(this);

    public bool Equals(DumbAction? other) => ModelResolver.Equals(this, other);

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        if (Append is { } append)
        {
            var items = world.GetValueOrDefault(AccountAddress, append.At, string.Empty);
            world[AccountAddress, append.At] = items == string.Empty ? append.Item : $"{items},{append.Item}";
        }

        if (Transfer is { } transfer)
        {
            if (transfer.From is { } from && transfer.To is { } to)
            {
                world.TransferAsset(
                    sender: from,
                    recipient: to,
                    value: FungibleAssetValue.Create(DumbCurrency, transfer.Amount));
            }
            else if (transfer.From is null && transfer.To is { } recipient)
            {
                world.MintAsset(
                    recipient: recipient,
                    value: FungibleAssetValue.Create(DumbCurrency, transfer.Amount));
            }
            else if (transfer.From is { } owner && transfer.To is null)
            {
                world.BurnAsset(
                    owner: owner,
                    value: FungibleAssetValue.Create(DumbCurrency, transfer.Amount));
            }
            else
            {
                throw new ArgumentException(
                    $"Both From and To cannot be null for {transfer}");
            }
        }

        if (Validators is { } validators)
        {
            world[AccountAddress, ValidatorsKey] = validators;
        }
    }
}
