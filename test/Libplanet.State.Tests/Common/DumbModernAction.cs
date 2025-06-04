using Libplanet.Serialization;
using Libplanet.Types;
using static Libplanet.State.SystemAddresses;

namespace Libplanet.State.Tests.Common;

[Model(Version = 1, TypeName = "Tests+DumbModernAction")]
public sealed record class DumbModernAction : ActionBase
{
    public static readonly DumbModernAction NoOp = DumbModernAction.Create();

    public static readonly Address DumbModernAddress =
        Address.Parse("0123456789abcdef0123456789abcdef12345678");

    public static readonly Currency DumbCurrency = Currency.Create("DUMB", 0);

    [Property(0)]
    public (Address At, string Item)? Append { get; init; }

    [Property(1)]
    public (Address? From, Address? To, BigInteger Amount)? Transfer { get; init; }

    [Property(2)]
    public ImmutableSortedSet<Validator>? Validators { get; init; }

    public static DumbModernAction Create(
        (Address At, string Item)? append = null,
        (Address? From, Address? To, BigInteger Amount)? transfer = null,
        ImmutableSortedSet<Validator>? validators = null,
        bool recordRandom = false)
    {
        if (transfer is { } t && t.From is null && t.To is null)
        {
            throw new ArgumentException(
                $"From and To of {nameof(transfer)} cannot both be null when " +
                $"{nameof(transfer)} is not null: {transfer}");
        }

        return new DumbModernAction()
        {
            Append = append,
            Transfer = transfer,
            Validators = validators,
        };
    }

    public override string ToString()
    {
        const string N = "null";
        const string E = "empty";
        string append = Append is { } a
            ? $"({a.At}, {a.Item})"
            : N;
        string transfer = Transfer is { } t
            ? $"({t.From?.ToString() ?? N}, {t.To?.ToString() ?? N}, {t.Amount})"
            : N;
        string validators = Validators is { } vs && vs.Any()
            ? string.Join(",", vs.Select(v => v.Address))
            : E;
        return $"{nameof(DumbModernAction)} {{ " +
            $"{nameof(Append)} = {append}, " +
            $"{nameof(Transfer)} = {transfer}, " +
            $"{nameof(Validators)} = {validators} " +
            $"}}";
    }

    protected override void OnExecute(IWorldContext world, IActionContext context)
    {
        if (Append is { } append)
        {
            var items = world.TryGetValue<string>(DumbModernAddress, append.At, out var value)
                ? $"{value},{append.Item}"
                : append.Item;
            world[DumbModernAddress, append.At] = items;
        }

        if (Transfer is { } transfer)
        {
            if (transfer.From is { } from && transfer.To is { } to)
            {
                world.TransferAsset(from, to, FungibleAssetValue.Create(DumbCurrency, transfer.Amount));
            }
            else if (transfer.To is { } to2)
            {
                world.MintAsset(to2, FungibleAssetValue.Create(DumbCurrency, transfer.Amount));
            }
            else if (transfer.From is { } from2)
            {
                world.BurnAsset(from2, FungibleAssetValue.Create(DumbCurrency, transfer.Amount));
            }
            else
            {
                throw new InvalidOperationException(
                    $"Both From and To cannot be null for {transfer}");
            }
        }

        if (Validators is { } validators)
        {
            world[SystemAccount, SystemAddresses.ValidatorsKey] = validators;
        }
    }
}
