using GraphQL.Types;
using Libplanet.Blockchain;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Explorer.Interfaces;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Explorer.Queries
{
    public class ExplorerQuery : ObjectGraphType
    {
        private static IBlockChainContext? _chainContext;

        public ExplorerQuery(IBlockChainContext chainContext)
        {
            _chainContext = chainContext;
            Field<BlockQuery>("blockQuery", resolve: context => new { });
            Field<TransactionQuery>("transactionQuery", resolve: context => new { });
            Field<EvidenceQuery>("evidenceQuery", resolve: context => new { });
            Field<StateQuery>("stateQuery", resolve: context => chainContext.BlockChain);
            Field<NonNullGraphType<NodeStateType>>("nodeState", resolve: context => chainContext);
            Field<HelperQuery>("helperQuery", resolve: context => new { });
            Field<RawStateQuery>("rawStateQuery", resolve: context => chainContext.BlockChain);

            Name = "ExplorerQuery";
        }

        private static IBlockChainContext ChainContext => _chainContext!;

        private static BlockChain Chain => ChainContext.BlockChain;

        private static Libplanet.Store.Store Store => ChainContext.Store;

        internal static IEnumerable<Block> ListBlocks(
            bool desc,
            int offset,
            int? limit)
        {
            Block tip = Chain.Tip;
            int tipIndex = tip.Height;

            var blocks = ListBlocks(
                Chain,
                desc ? tipIndex - offset - (limit ?? 100) : offset,
                limit ?? 100);
            return desc ? blocks.OrderByDescending(x => x.Height)
                : blocks.OrderBy(x => x.Height);
        }

        internal static IEnumerable<Transaction> ListTransactions(
            Address? signer, bool desc, int offset, int? limit)
        {
            Block tip = Chain.Tip;
            int tipIndex = tip.Height;

            if (offset < 0)
            {
                offset = tipIndex + offset + 1;
            }

            if (tipIndex < offset || offset < 0)
            {
                yield break;
            }

            Block? block = Chain[desc ? tipIndex - offset : offset];
            while (block is not null && limit is null or > 0)
            {
                foreach (var tx in desc ? block.Transactions.Reverse() : block.Transactions)
                {
                    if (IsValidTransaction(tx, signer))
                    {
                        yield return tx;
                        limit--;
                        if (limit <= 0)
                        {
                            break;
                        }
                    }
                }

                block = GetNextBlock(block, desc);
            }
        }

        internal static IEnumerable<Transaction> ListStagedTransactions(
            Address? signer, bool desc, int offset, int? limit)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offset),
                    $"{nameof(ListStagedTransactions)} doesn't support negative offset.");
            }

            var stagedTxs = Chain.StagedTransactions.Iterate()
                .Where(tx => IsValidTransaction(tx, signer))
                .Skip(offset);

            stagedTxs = desc ? stagedTxs.OrderByDescending(tx => tx.Timestamp)
                : stagedTxs.OrderBy(tx => tx.Timestamp);

            stagedTxs = stagedTxs.TakeWhile((tx, index) => limit is null || index < limit);

            return stagedTxs;
        }

        internal static IEnumerable<EvidenceBase> ListPendingEvidence(
            bool desc, int offset, int? limit)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offset),
                    $"{nameof(ListPendingEvidence)} doesn't support negative offset.");
            }

            var blockChain = Chain;
            var comparer = desc ? EvidenceIdComparer.Descending : EvidenceIdComparer.Ascending;
            var evidence = blockChain.PendingEvidences.Values
                                      .Skip(offset)
                                      .Take(limit ?? int.MaxValue)
                                      .OrderBy(ev => ev.Id, comparer);

            return evidence;
        }

        internal static IEnumerable<EvidenceBase> ListCommitEvidence(
            BlockHash? blockHash, bool desc, int offset, int? limit)
        {
            var blockChain = Chain;
            var block = blockHash != null ? blockChain[blockHash.Value] : blockChain.Tip;
            var comparer = desc ? EvidenceIdComparer.Descending : EvidenceIdComparer.Ascending;
            var evidence = block.Evidences
                                 .Skip(offset)
                                 .Take(limit ?? int.MaxValue)
                                 .OrderBy(ev => ev.Id, comparer);

            return evidence;
        }

        internal static Block? GetBlockByHash(BlockHash hash) => Store.GetBlock(hash);

        internal static Block GetBlockByIndex(int index) => Chain[index];

        internal static Transaction GetTransaction(TxId id) => Chain.GetTransaction(id);

        internal static EvidenceBase GetEvidence(EvidenceId id) => Chain.CommittedEvidences[id];

        private static Block? GetNextBlock(Block block, bool desc)
        {
            if (desc && block.PreviousHash is { } prev)
            {
                return Chain[prev];
            }
            else if (!desc && block != Chain.Tip)
            {
                return Chain[block.Height + 1];
            }

            return null;
        }

        private static IEnumerable<Block> ListBlocks(BlockChain chain, int from, int limit)
        {
            if (chain.Tip.Height < from)
            {
                return new List<Block>();
            }

            var count = (int)Math.Min(limit, chain.Tip.Height - from + 1);
            var blocks = Enumerable.Range(0, count)
                .Select(offset => chain[from + offset])
                .OrderBy(block => block.Height);

            return blocks;
        }

        private static bool IsValidTransaction(
            Transaction tx,
            Address? signer)
        {
            if (signer is { } signerVal)
            {
                return tx.Signer.Equals(signerVal);
            }

            return true;
        }
    }
}
