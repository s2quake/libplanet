using System;
using System.Collections.Immutable;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;

namespace Libplanet.Types.Tx;

public interface ITxInvoice : IEquatable<ITxInvoice>
{
    ImmutableSortedSet<Address> UpdatedAddresses { get; }

    DateTimeOffset Timestamp { get; }

    BlockHash? GenesisHash { get; }

    ImmutableArray<IValue> Actions { get; }

    FungibleAssetValue? MaxGasPrice { get; }

    long? GasLimit { get; }
}
