using Libplanet;
using Libplanet.Types.Transactions;

namespace Libplanet.Node.Services;

internal sealed class TransactionService(IBlockChainService blockChainService)
{
    private readonly Blockchain _blockChain = blockChainService.BlockChain;

    public void StageTransaction(Transaction transaction) =>
        _blockChain.StagedTransactions.Add(transaction);
}
