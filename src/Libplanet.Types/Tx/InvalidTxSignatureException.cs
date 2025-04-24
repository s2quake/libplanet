namespace Libplanet.Types.Tx;

public class InvalidTxSignatureException(string message, TxId txid)
    : InvalidTxException(message, txid)
{
}
