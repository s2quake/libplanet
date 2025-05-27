namespace Libplanet.Data;

public sealed class CommittedTransactionIndex(IDatabase database)
    : TransactionIndexBase(database, "committed_tx")
{
}
