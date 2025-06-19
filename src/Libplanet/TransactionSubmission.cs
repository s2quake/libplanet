using Libplanet.State;
using Libplanet.Types;

namespace Libplanet;

public sealed record class TransactionSubmission
{
    public required ISigner Signer { get; init; }

    public IAction[] Actions { get; init; } = [];

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public FungibleAssetValue? MaxGasPrice { get; init; }

    public long GasLimit { get; init; }
}
