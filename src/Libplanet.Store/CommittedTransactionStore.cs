namespace Libplanet.Store;

public sealed class CommittedTransactionStore(IDatabase database)
    : TransactionStoreBase(database, "committed_tx")
{
}
