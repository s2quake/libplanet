namespace Libplanet.Data;

public sealed class CommittedTransactionStore(IDatabase database)
    : TransactionStoreBase(database, "committed_tx")
{
}
