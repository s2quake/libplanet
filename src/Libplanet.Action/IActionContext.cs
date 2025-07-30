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

    TxId TxId { get; }

    Address Proposer { get; }

    long BlockHeight { get; }

    int BlockProtocolVersion { get; }

    BlockCommit LastCommit { get; }

    int RandomSeed { get; }

    FungibleAssetValue MaxGasPrice { get; }

    ImmutableSortedSet<Transaction> Txs { get; }

    ImmutableSortedSet<EvidenceBase> Evidence { get; }

    IRandom GetRandom();
}
