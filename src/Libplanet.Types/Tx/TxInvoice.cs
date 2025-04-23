using System;
using System.Collections.Immutable;
using System.Linq;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Tx;

public sealed record class TxInvoice : ITxInvoice, IEquatable<TxInvoice>
{
    public TxInvoice()
    {
    }

    public TxInvoice(ITxInvoice invoice)
    {
        Actions = invoice.Actions;
        GenesisHash = invoice.GenesisHash;
        UpdatedAddresses = invoice.UpdatedAddresses;
        Timestamp = invoice.Timestamp;
        MaxGasPrice = invoice.MaxGasPrice;
        GasLimit = invoice.GasLimit;
    }

    public ImmutableArray<IValue> Actions { get; init; } = [];

    public BlockHash? GenesisHash { get; init; }

    public ImmutableSortedSet<Address> UpdatedAddresses { get; init; } = [];

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public FungibleAssetValue? MaxGasPrice { get; init; }

    public long? GasLimit { get; init; }

    public void Verify()
    {
        switch (MaxGasPrice, GasLimit)
        {
            case (null, null):
                break;
            case (null, { }):
            case ({ }, null):
                throw new ArgumentException(
                    $"Either {nameof(MaxGasPrice)} (null: {MaxGasPrice is null}) and " +
                    $"{nameof(GasLimit)} (null: {GasLimit is null}) must be both null " +
                    $"or both non-null.");
            case ({ } mgp, { } gl):
                if (mgp.Sign < 0 || gl < 0)
                {
                    throw new ArgumentException(
                        $"Both {nameof(MaxGasPrice)} ({mgp}) and {nameof(GasLimit)} ({gl}) " +
                        $"must be non-negative.");
                }

                break;
        }
    }

    bool IEquatable<ITxInvoice>.Equals(ITxInvoice? other) =>
        other is { } o &&
        (o.GenesisHash is { } otherGenesisHash
            ? otherGenesisHash.Equals(GenesisHash)
            : GenesisHash is null) &&
        o.UpdatedAddresses.SetEquals(UpdatedAddresses) &&
        o.Timestamp.Equals(Timestamp) &&
        o.Actions.Equals(Actions) &&
        o.MaxGasPrice.Equals(MaxGasPrice) &&
        o.GasLimit.Equals(GasLimit);

    public bool Equals(TxInvoice? other) =>
        other is ITxInvoice otherInvoice && otherInvoice.Equals(this);

    public override int GetHashCode() =>
        HashCode.Combine(
            GenesisHash,
            UpdatedAddresses,
            Timestamp,
            Actions,
            MaxGasPrice,
            GasLimit);

    public override string ToString()
    {
        string actions = Actions.ToString() ?? string.Empty;
        string indentedActions = actions.Replace("\n", "\n  ");
        return nameof(TxInvoice) + " {\n" +
            $"  {nameof(UpdatedAddresses)} = ({UpdatedAddresses.Count})" +
            (UpdatedAddresses.Any()
                ? $"\n    {string.Join("\n    ", UpdatedAddresses)},\n"
                : ",\n") +
            $"  {nameof(Timestamp)} = {Timestamp},\n" +
            $"  {nameof(GenesisHash)} = {GenesisHash?.ToString() ?? "(null)"},\n" +
            $"  {nameof(Actions)} = {indentedActions},\n" +
            "}";
    }
}
