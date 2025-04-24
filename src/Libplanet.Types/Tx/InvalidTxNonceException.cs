namespace Libplanet.Types.Tx;

public sealed class InvalidTxNonceException(
    string message, TxId txId, long expectedNonce, long improperNonce)
    : InvalidTxException(message, txId)
{
    public long ExpectedNonce { get; } = expectedNonce;

    public long ImproperNonce { get; } = improperNonce;
}
