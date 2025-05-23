using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Transactions;

namespace Libplanet.Action;

public interface IActionContext
{
    Address Signer { get; }

    TxId TxId { get; }

    Address Proposer { get; }

    int BlockHeight { get; }

    int BlockProtocolVersion { get; }

    BlockCommit LastCommit { get; }

    int RandomSeed { get; }

    FungibleAssetValue MaxGasPrice { get; }

    IRandom GetRandom();
}
