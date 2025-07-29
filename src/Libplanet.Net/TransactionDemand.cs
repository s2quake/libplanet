using Libplanet.Types;

namespace Libplanet.Net;

public sealed record class TransactionDemand(Peer Peer, ImmutableHashSet<TxId> TxIds)
{
}
