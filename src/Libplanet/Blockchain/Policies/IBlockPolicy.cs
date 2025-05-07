using Libplanet.Action;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain.Policies;

public interface IBlockPolicy
{
    PolicyActions PolicyActions { get; }

    InvalidOperationException? ValidateNextBlockTx(BlockChain blockChain, Transaction transaction);

    Exception? ValidateNextBlock(BlockChain blockChain, Block nextBlock);

    long GetMaxTransactionsBytes(long index);

    int GetMinTransactionsPerBlock(long index);

    int GetMaxTransactionsPerBlock(long index);

    int GetMaxTransactionsPerSignerPerBlock(long index);

    long GetMaxEvidencePendingDuration(long index);
}
