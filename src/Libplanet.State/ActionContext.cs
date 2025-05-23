using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Transactions;

namespace Libplanet.State;

internal sealed record class ActionContext : IActionContext
{
    public Address Signer { get; init; }

    public TxId TxId { get; init; }

    public Address Proposer { get; init; }

    public int BlockHeight { get; init; }

    public int BlockProtocolVersion { get; init; }

    public BlockCommit LastCommit { get; init; } = BlockCommit.Empty;

    public int RandomSeed { get; init; }

    public FungibleAssetValue MaxGasPrice { get; init; }

    public IRandom GetRandom() => new Random(RandomSeed);
}
