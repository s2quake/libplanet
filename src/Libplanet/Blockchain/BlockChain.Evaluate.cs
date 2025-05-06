using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Store;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    public HashDigest<SHA256> DetermineNextBlockStateRootHash(
        Block block, out IReadOnlyList<CommittedActionEvaluation> evaluations)
    {
        _rwlock.EnterWriteLock();
        try
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            evaluations = EvaluateBlock(block);

            _logger.Debug(
                "Took {DurationMs} ms to evaluate block #{BlockHeight} " +
                "hash {Hash} with {Count} action evaluations",
                stopwatch.ElapsedMilliseconds,
                block.Height,
                block.Hash,
                evaluations.Count);

            if (evaluations.Count > 0)
            {
                return evaluations[^1].OutputState;
            }
            else
            {
                return Store.GetStateRootHash(block.Hash) is { } stateRootHash
                    ? stateRootHash
                    : StateStore.GetStateRoot(default).Hash;
            }
        }
        finally
        {
            _rwlock.ExitWriteLock();
        }
    }

    public IReadOnlyList<CommittedActionEvaluation> EvaluateBlock(Block block) =>
            ActionEvaluator.Evaluate(
                (RawBlock)block,
                Store.GetStateRootHash(block.Hash));

    internal Block EvaluateAndSign(
        RawBlock rawBlock, PrivateKey privateKey)
    {
        if (rawBlock.Height < 1)
        {
            throw new ArgumentException(
                $"Given {nameof(rawBlock)} must have block height " +
                $"higher than 0");
        }
        else
        {
            var prevBlock = _blocks[rawBlock.PreviousHash];
            var stateRootHash = GetNextStateRootHash(prevBlock.Hash)
                ?? throw new NullReferenceException(
                    $"State root hash of block is not prepared");
            return rawBlock.Sign(privateKey, stateRootHash);
        }
    }

    internal HashDigest<SHA256> DetermineBlockPrecededStateRootHash(
        RawBlock rawBlock, out IReadOnlyList<CommittedActionEvaluation> evaluations)
    {
        _rwlock.EnterWriteLock();
        try
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            evaluations = EvaluateBlockPrecededStateRootHash(rawBlock);

            _logger.Debug(
                "Took {DurationMs} ms to evaluate block #{BlockHeight} " +
                "pre-evaluation hash {RawHash} with {Count} action evaluations",
                stopwatch.ElapsedMilliseconds,
                rawBlock.Height,
                rawBlock.RawHash,
                evaluations.Count);

            if (evaluations.Count > 0)
            {
                return evaluations[^1].OutputState;
            }
            else
            {
                return Store.GetStateRootHash(rawBlock.PreviousHash) is { } prevStateRootHash
                    ? prevStateRootHash
                    : StateStore.GetStateRoot(default).Hash;
            }
        }
        finally
        {
            _rwlock.ExitWriteLock();
        }
    }

    internal IReadOnlyList<CommittedActionEvaluation> EvaluateBlockPrecededStateRootHash(RawBlock rawBlock)
        => ActionEvaluator.Evaluate(rawBlock, Store.GetStateRootHash(rawBlock.PreviousHash));
}
