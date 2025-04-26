using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Action;

public interface IActionContext
{
    Address Signer { get; }

    TxId? TxId { get; }

    Address Miner { get; }

    long BlockHeight { get; }

    int BlockProtocolVersion { get; }

    BlockCommit? LastCommit { get; }

    IWorld World { get; }

    int RandomSeed { get; }

    bool IsPolicyAction { get; }

    FungibleAssetValue? MaxGasPrice { get; }

    ImmutableSortedSet<Transaction> Txs { get; }

    ImmutableSortedSet<EvidenceBase> Evidence { get; }

    IRandom GetRandom();
}
