namespace Libplanet.Types.Tx;

public sealed class TxPolicyViolationException : InvalidTxException
{
    public TxPolicyViolationException(string message, TxId txid)
        : base(message, txid)
    {
    }

    public TxPolicyViolationException(string message, TxId txid, Exception innerException)
        : base(message, txid, innerException)
    {
    }
}
