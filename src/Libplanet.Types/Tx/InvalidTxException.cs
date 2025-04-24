using System;

namespace Libplanet.Types.Tx;

public abstract class InvalidTxException : Exception
{
    protected InvalidTxException(string message, TxId txid)
        : base($"{txid}: {message}")
    {
        TxId = txid;
    }

    protected InvalidTxException(string message, TxId txid, Exception innerException)
        : base($"{txid}: {message}", innerException)
    {
        TxId = txid;
    }

    public TxId TxId { get; }
}
