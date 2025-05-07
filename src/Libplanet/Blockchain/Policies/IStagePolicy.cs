using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain.Policies;

public interface IStagePolicy
{
    public bool Stage(BlockChain blockChain, Transaction transaction);

    public bool Unstage(BlockChain blockChain, TxId txId);

    public bool Ignore(BlockChain blockChain, TxId txId);

    public bool Ignores(BlockChain blockChain, TxId txId);

    public Transaction Get(BlockChain blockChain, TxId txId, bool filtered = true);

    public ImmutableArray<Transaction> Iterate(BlockChain blockChain, bool filtered = true);

    public long GetNextTxNonce(BlockChain blockChain, Address address);
}
