using Libplanet.Action;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    internal TxExecution[] MakeTxExecutions(Block block, ActionEvaluation[] evaluations)
    {
        var query = from evaluation in evaluations
                    group evaluation by evaluation.InputContext.TxId into @group
                    select new TxExecution
                    {
                        TxId = @group.Key,
                        BlockHash = block.BlockHash,
                        InputState = @group.First().InputWorld.Trie.Hash,
                        OutputState = @group.Last().OutputWorld.Trie.Hash,
                        ExceptionNames = [.. @group.Select(item => item.ExceptionMessage)],
                    };

        return [.. query];
    }
}
