using Libplanet;
using Libplanet.Types;

namespace Libplanet.Node.Services;

internal sealed class TransactionService(IBlockChainService blockChainService)
{
    private readonly Blockchain _blockChain = blockChainService.BlockChain;

    public void StageTransaction(Transaction transaction) =>
        _blockChain.StagedTransactions.Add(transaction);
}
