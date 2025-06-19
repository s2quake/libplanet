using Libplanet.State;
using Libplanet.Types;

namespace Libplanet;

public sealed record class TransactionSubmission
{
    public IAction[] Actions { get; init; } = [];

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public FungibleAssetValue? MaxGasPrice { get; init; }

    public long GasLimit { get; init; }
}
