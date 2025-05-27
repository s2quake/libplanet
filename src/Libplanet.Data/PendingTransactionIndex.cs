namespace Libplanet.Data;

public sealed class PendingTransactionIndex(IDatabase database)
    : TransactionIndexBase(database, "pending_tx")
{
}
