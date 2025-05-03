using Libplanet.Types.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Action;

internal sealed record class ActionContext : IActionContext
{
    public Address Signer { get; init; }

    public TxId TxId { get; init; }

    public Address Proposer { get; init; }

    public long BlockHeight { get; init; }

    public int BlockProtocolVersion { get; init; }

    public BlockCommit LastCommit { get; init; } = BlockCommit.Empty;

    public int RandomSeed { get; init; }

    public FungibleAssetValue MaxGasPrice { get; init; }

    public ImmutableSortedSet<Transaction> Txs { get; init; } = [];

    public ImmutableSortedSet<EvidenceBase> Evidence { get; init; } = [];

    public IRandom GetRandom() => new Random(RandomSeed);
}
