namespace Libplanet.Data;

public sealed class CommittedTransactionIndex(IDatabase database, int cacheSize = 100)
    : TransactionIndexBase(database, "committed_tx", cacheSize)
{
}
