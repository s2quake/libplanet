using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain.Policies;

public interface IStagePolicy
{
    public bool Stage(Transaction transaction);

    public bool Unstage(TxId txId);

    public bool Ignore(TxId txId);

    public bool Ignores(TxId txId);

    public Transaction Get(TxId txId, bool filtered = true);

    public ImmutableArray<Transaction> Iterate(bool filtered = true);

    public long GetNextTxNonce(Address address);
}
