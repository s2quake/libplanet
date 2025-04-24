using System;
using System.Collections.Immutable;
using System.Linq;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Tx;

[Model(Version = 1)]
public sealed record class TxInvoice : IEquatable<TxInvoice>
{
    [Property(0)]
    public ImmutableArray<IValue> Actions { get; init; } = [];

    [Property(1)]
    public BlockHash? GenesisHash { get; init; }

    [Property(2)]
    public ImmutableSortedSet<Address> UpdatedAddresses { get; init; } = [];

    [Property(3)]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [Property(4)]
    public FungibleAssetValue? MaxGasPrice { get; init; }

    [Property(5)]
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

    public bool Equals(TxInvoice? other) => ModelUtility.Equals(this, other);

    public override int GetHashCode() => ModelUtility.GetHashCode(this);
}
