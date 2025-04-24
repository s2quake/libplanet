namespace Libplanet.Types.Tx;

public sealed class InvalidTxIdException(string message, TxId txid)
    : InvalidTxException(message, txid)
{
}
