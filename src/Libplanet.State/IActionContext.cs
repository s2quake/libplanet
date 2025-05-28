using Libplanet.Types;

namespace Libplanet.State;

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
