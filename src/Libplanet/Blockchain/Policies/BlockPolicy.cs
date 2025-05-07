using System.Diagnostics.Contracts;
using Libplanet.Action;
using Libplanet.Serialization;
using Libplanet.Types.Blocks;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain.Policies;

public class BlockPolicy : IBlockPolicy
{
    public static readonly TimeSpan DefaultTargetBlockInterval = TimeSpan.FromSeconds(5);

    private readonly Func<BlockChain, Transaction, InvalidOperationException?> _validateNextBlockTx;

    private readonly Func<BlockChain, Block, Exception?> _validateNextBlock;

    private readonly PolicyActions _policyActions;
    private readonly Func<long, long> _getMaxTransactionsBytes;
    private readonly Func<long, int> _getMinTransactionsPerBlock;
    private readonly Func<long, int> _getMaxTransactionsPerBlock;
    private readonly Func<long, int> _getMaxTransactionsPerSignerPerBlock;
    private readonly Func<long, long> _getMaxEvidencePendingDuration;

    public BlockPolicy(
        PolicyActions? policyActions = null,
        TimeSpan? blockInterval = null,
        Func<BlockChain, Transaction, InvalidOperationException?>?
            validateNextBlockTx = null,
        Func<BlockChain, Block, Exception?>?
            validateNextBlock = null,
        Func<long, long>? getMaxTransactionsBytes = null,
        Func<long, int>? getMinTransactionsPerBlock = null,
        Func<long, int>? getMaxTransactionsPerBlock = null,
        Func<long, int>? getMaxTransactionsPerSignerPerBlock = null,
        Func<long, long>? getMaxEvidencePendingDuration = null)
    {
        _policyActions = policyActions ?? new PolicyActions();
        BlockInterval = blockInterval ?? DefaultTargetBlockInterval;
        _getMaxTransactionsBytes = getMaxTransactionsBytes ?? (_ => 100L * 1024L);
        _getMinTransactionsPerBlock = getMinTransactionsPerBlock ?? (_ => 0);
        _getMaxTransactionsPerBlock = getMaxTransactionsPerBlock ?? (_ => 100);
        _getMaxTransactionsPerSignerPerBlock = getMaxTransactionsPerSignerPerBlock
            ?? GetMaxTransactionsPerBlock;
        _getMaxEvidencePendingDuration = getMaxEvidencePendingDuration ?? (_ => 10L);

        _validateNextBlockTx = validateNextBlockTx ?? ((_, __) => null);
        if (validateNextBlock is { } vnb)
        {
            _validateNextBlock = vnb;
        }
        else
        {
            _validateNextBlock = (blockchain, block) =>
            {
                long maxTransactionsBytes = GetMaxTransactionsBytes(block.Height);
                int minTransactionsPerBlock = GetMinTransactionsPerBlock(block.Height);
                int maxTransactionsPerBlock = GetMaxTransactionsPerBlock(block.Height);
                int maxTransactionsPerSignerPerBlock =
                    GetMaxTransactionsPerSignerPerBlock(block.Height);
                long maxEvidencePendingDuration = GetMaxEvidencePendingDuration(block.Height);

                long blockBytes = ModelSerializer.Serialize(block.Transactions)
                    .EncodingLength;
                if (blockBytes > maxTransactionsBytes)
                {
                    return new InvalidOperationException(
                        $"The size of block #{block.Height} {block.BlockHash} is too large where " +
                        $"the maximum number of bytes allowed is {maxTransactionsBytes}: " +
                        $"{blockBytes}.");
                }
                else if (block.Transactions.Count < minTransactionsPerBlock)
                {
                    return new InvalidOperationException(
                        $"Block #{block.Height} {block.BlockHash} should include " +
                        $"at least {minTransactionsPerBlock} transaction(s): " +
                        $"{block.Transactions.Count}");
                }
                else if (block.Transactions.Count > maxTransactionsPerBlock)
                {
                    return new InvalidOperationException(
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
                        return new InvalidOperationException(
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
                    return new InvalidOperationException(
                        $"Block #{block.Height} {block.BlockHash} includes evidence" +
                        $"that is older than expiration height {evidenceExpirationHeight}");
                }

                return null;
            };
        }
    }

    public PolicyActions PolicyActions => _policyActions;

    public TimeSpan BlockInterval { get; }

    public virtual InvalidOperationException? ValidateNextBlockTx(BlockChain blockChain, Transaction transaction)
    {
        return _validateNextBlockTx(blockChain, transaction);
    }

    public virtual Exception? ValidateNextBlock(
    BlockChain blockChain,
    Block nextBlock)
    {
        return _validateNextBlock(blockChain, nextBlock);
    }

    [Pure]
    public long GetMaxTransactionsBytes(long index) => _getMaxTransactionsBytes(index);

    [Pure]
    public int GetMinTransactionsPerBlock(long index) => _getMinTransactionsPerBlock(index);

    [Pure]
    public int GetMaxTransactionsPerBlock(long index) => _getMaxTransactionsPerBlock(index);

    [Pure]
    public int GetMaxTransactionsPerSignerPerBlock(long index)
    => _getMaxTransactionsPerSignerPerBlock(index);

    [Pure]
    public long GetMaxEvidencePendingDuration(long index)
    => _getMaxEvidencePendingDuration(index);
}
