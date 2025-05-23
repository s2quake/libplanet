namespace Libplanet.Data;

public sealed class PendingTransactionStore(IDatabase database)
    : TransactionStoreBase(database, "pending_tx")
{
}
