#nullable enable
using System.Numerics;
using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;

namespace Libplanet.Action.Tests.Common
{
    public sealed class DumbAction : IAction
    {
        public static readonly DumbAction NoOp = DumbAction.Create();

        public static readonly Text TypeId = new Text(nameof(DumbAction));

        public static readonly Currency DumbCurrency = new("DUMB", 0);

        public DumbAction()
        {
        }

        public (Address At, string Item)? Append { get; private set; }

        public (Address? From, Address? To, BigInteger Amount)? Transfer { get; private set; }

        public ImmutableList<Validator>? Validators { get; private set; }

        public IValue PlainValue
        {
            get
            {
                var plainValue = Dictionary.Empty
                    .Add("type_id", TypeId);
                if (Append is { } set)
                {
                    plainValue = plainValue
                        .Add("target_address", set.At.ToBencodex())
                        .Add("item", set.Item);
                }

                if (Transfer is { } transfer)
                {
                    plainValue = plainValue
                        .Add("transfer_from", transfer.From?.ToBencodex() ?? Null.Value)
                        .Add("transfer_to", transfer.To?.ToBencodex() ?? Null.Value)
                        .Add("transfer_amount", transfer.Amount);
                }

                if (Validators is { } validators)
                {
                    plainValue = plainValue
                        .Add("validators", new List(validators.Select(v => v.Bencoded)));
                }

                return plainValue;
            }
        }

        public static DumbAction Create(
            (Address At, string Item)? append = null,
            (Address? From, Address? To, BigInteger Amount)? transfer = null,
            IEnumerable<Validator>? validators = null,
            bool recordRandom = false)
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
                Validators = validators?.ToImmutableList(),
            };
        }

        public IWorld Execute(IActionContext context)
        {
            IWorld world = context.PreviousState;

            if (Append is { } append)
            {
                IAccount account = world.GetAccount(ReservedAddresses.LegacyAccount);
                string? items = (Text?)account.GetState(append.At);
                items = items is null ? append.Item : $"{items},{append.Item}";
                account = account.SetState(append.At, (Text)items!);
                world = world.SetAccount(ReservedAddresses.LegacyAccount, account);
            }

            if (Transfer is { } transfer)
            {
                world = (transfer.From, transfer.To) switch
                {
                    (Address from, Address to) => world.TransferAsset(
                        context,
                        sender: from,
                        recipient: to,
                        value: new FungibleAssetValue(DumbCurrency, transfer.Amount)),
                    (null, Address to) => world.MintAsset(
                        context,
                        recipient: to,
                        value: new FungibleAssetValue(DumbCurrency, transfer.Amount)),
                    (Address from, null) => world.BurnAsset(
                        context,
                        owner: from,
                        value: new FungibleAssetValue(DumbCurrency, transfer.Amount)),
                    _ => throw new ArgumentException(
                        $"Both From and To cannot be null for {transfer}"),
                };
            }

            if (Validators is { } validators)
            {
                world = world.SetValidatorSet(new ValidatorSet(validators.ToList()));
            }

            return world;
        }

        public void LoadPlainValue(IValue plainValue) => LoadPlainValue((Dictionary)plainValue);

        public void LoadPlainValue(Dictionary plainValue)
        {
            if (!plainValue["type_id"].Equals(TypeId))
            {
                throw new ArgumentException(
                    $"An invalid form of {nameof(plainValue)} was given: {plainValue}");
            }

            if (plainValue.TryGetValue((Text)"target_address", out IValue at) &&
                plainValue.TryGetValue((Text)"item", out IValue item) &&
                item is Text i)
            {
                Append = (Address.Create(at), i);
            }

            if (plainValue.TryGetValue((Text)"transfer_from", out IValue f) &&
                plainValue.TryGetValue((Text)"transfer_to", out IValue t) &&
                plainValue.TryGetValue((Text)"transfer_amount", out IValue a) &&
                a is Integer amount)
            {
                Address? from = f is Null ? null : Address.Create(f);
                Address? to = t is Null ? null : Address.Create(t);
                Transfer = (from, to, amount.Value);
            }

            if (plainValue.ContainsKey((Text)"validators"))
            {
                Validators = ((List)plainValue["validators"])
                    .Select(value => new Validator(value))
                    .ToImmutableList();
            }
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
                ? string.Join(",", vs.Select(v => v.OperatorAddress))
                : E;
            return $"{nameof(DumbAction)} {{ " +
                $"{nameof(Append)} = {append}, " +
                $"{nameof(Transfer)} = {transfer}, " +
                $"{nameof(Validators)} = {validators} " +
                $"}}";
        }
    }
}
