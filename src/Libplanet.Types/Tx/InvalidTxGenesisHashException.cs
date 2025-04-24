using Libplanet.Types.Blocks;

namespace Libplanet.Types.Tx;

public sealed class InvalidTxGenesisHashException(
    string message, TxId txid, BlockHash expectedGenesisHash, BlockHash? improperGenesisHash)
    : InvalidTxException(
        $"{message}\n" +
            $"Expected genesis hash: {expectedGenesisHash}\n" +
            $"Improper genesis hash: {improperGenesisHash}",
        txid)
{
    public BlockHash ExpectedGenesisHash { get; } = expectedGenesisHash;

    public BlockHash? ImproperGenesisHash { get; } = improperGenesisHash;
}
