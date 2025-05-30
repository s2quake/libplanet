namespace Libplanet.Data;

public sealed class PendingTransactionIndex(IDatabase database, int cacheSize = 100)
    : TransactionIndexBase(database, "pending_tx", cacheSize)
{
}
