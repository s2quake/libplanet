using Libplanet.Action;
using Libplanet.Serialization;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain;

public sealed record class BlockChainOptions
{
    public IStore Store { get; init; } = new MemoryStore();

    public IKeyValueStore KeyValueStore { get; init; } = new MemoryKeyValueStore();

    public PolicyActions PolicyActions { get; init; } = PolicyActions.Empty;

    public TimeSpan BlockInterval { get; init; } = TimeSpan.FromSeconds(5);

    public long MaxTransactionsBytes { get; init; } = 100L * 1024L;

    public int MinTransactionsPerBlock { get; init; } = 0;

    public int MaxTransactionsPerBlock { get; init; } = 100;

    public int MaxTransactionsPerSignerPerBlock { get; init; } = 100;

    public long MaxEvidencePendingDuration { get; init; } = 10L;

    public Action<BlockChain, Block>? BlockValidation { get; init; }

    public Action<BlockChain, Transaction>? TransactionValidation { get; init; }

    internal void ValidateTransaction(BlockChain blockChain, Transaction transaction)
        => TransactionValidation?.Invoke(blockChain, transaction);

    internal void ValidateBlock(BlockChain blockChain, Block block)
    {
        BlockValidation?.Invoke(blockChain, block);

        long maxTransactionsBytes = MaxTransactionsBytes;
        int minTransactionsPerBlock = MinTransactionsPerBlock;
        int maxTransactionsPerBlock = MaxTransactionsPerBlock;
        int maxTransactionsPerSignerPerBlock = MaxTransactionsPerSignerPerBlock;
        long maxEvidencePendingDuration = MaxEvidencePendingDuration;

        long blockBytes = ModelSerializer.Serialize(block.Transactions)
            .EncodingLength;
        if (blockBytes > maxTransactionsBytes)
        {
            throw new InvalidOperationException(
                $"The size of block #{block.Height} {block.BlockHash} is too large where " +
                $"the maximum number of bytes allowed is {maxTransactionsBytes}: " +
                $"{blockBytes}.");
        }
        else if (block.Transactions.Count < minTransactionsPerBlock)
        {
            throw new InvalidOperationException(
                $"Block #{block.Height} {block.BlockHash} should include " +
                $"at least {minTransactionsPerBlock} transaction(s): " +
                $"{block.Transactions.Count}");
        }
        else if (block.Transactions.Count > maxTransactionsPerBlock)
        {
            throw new InvalidOperationException(
                $"Block #{block.Height} {block.BlockHash} should include " +
                $"at most {maxTransactionsPerBlock} transaction(s): " +
                $"{block.Transactions.Count}");
        }
        else
        {
            var groups = block.Transactions
                .GroupBy(tx => tx.Signer)
                .Where(group => group.Count() > maxTransactionsPerSignerPerBlock);
            if (groups.FirstOrDefault() is { } offendingGroup)
            {
                int offendingGroupCount = offendingGroup.Count();
                throw new InvalidOperationException(
                    $"Block #{block.Height} {block.BlockHash} includes too many " +
                    $"transactions from signer {offendingGroup.Key} where " +
                    $"the maximum number of transactions allowed by a single signer " +
                    $"per block is {maxTransactionsPerSignerPerBlock}: " +
                    $"{offendingGroupCount}");
            }
        }

        long evidenceExpirationHeight = block.Height - maxEvidencePendingDuration;
        if (block.Evidences.Any(evidence => evidence.Height < evidenceExpirationHeight))
        {
            throw new InvalidOperationException(
                $"Block #{block.Height} {block.BlockHash} includes evidence" +
                $"that is older than expiration height {evidenceExpirationHeight}");
        }
    }

}
