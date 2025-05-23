using Libplanet.State;
using Libplanet.Types.Assets;
using Libplanet.Types.Crypto;

namespace Libplanet.Blockchain;

public sealed record class TransactionSubmission
{
    public required PrivateKey Signer { get; init; }

    public IAction[] Actions { get; init; } = [];

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public FungibleAssetValue? MaxGasPrice { get; init; }

    public long GasLimit { get; init; }
}
