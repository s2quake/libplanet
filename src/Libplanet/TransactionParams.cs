using Libplanet.State;
using Libplanet.Types;

namespace Libplanet;

public sealed record class TransactionParams
{
    public IAction[] Actions { get; init; } = [];

    public DateTimeOffset Timestamp { get; init; }

    public FungibleAssetValue? MaxGasPrice { get; init; }

    public long GasLimit { get; init; }
}
