using System.Diagnostics;
using System.Security.Cryptography;
using Libplanet.Action;
using Libplanet.Store;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    public HashDigest<SHA256> DetermineNextBlockStateRootHash(
        Block block, out CommittedActionEvaluation[] evaluations)
    {
        evaluations = EvaluateBlock(block);

        if (evaluations.Length > 0)
        {
            return evaluations[^1].OutputState;
        }

        return Store.GetStateRootHash(block.BlockHash) is { } stateRootHash
            ? stateRootHash
            : StateStore.GetStateRoot(default).Hash;
    }

    public CommittedActionEvaluation[] EvaluateBlock(Block block)
        => ActionEvaluator.Evaluate((RawBlock)block, Store.GetStateRootHash(block.BlockHash));

    internal Block EvaluateAndSign(RawBlock rawBlock, PrivateKey privateKey)
    {
        if (rawBlock.Header.Height < 1)
        {
            throw new ArgumentException(
                $"Given {nameof(rawBlock)} must have block height " +
                $"higher than 0");
        }
        else
        {
            var prevBlock = Blocks[rawBlock.Header.PreviousHash];
            var stateRootHash = GetNextStateRootHash(prevBlock.BlockHash)
                ?? throw new NullReferenceException(
                    $"State root hash of block is not prepared");
            return rawBlock.Sign(privateKey, stateRootHash);
        }
    }

    internal HashDigest<SHA256> DetermineBlockPrecededStateRootHash(
        RawBlock rawBlock, out CommittedActionEvaluation[] evaluations)
    {
        _rwlock.EnterWriteLock();
        try
        {
            evaluations = EvaluateBlockPrecededStateRootHash(rawBlock);

            if (evaluations.Length > 0)
            {
                return evaluations[^1].OutputState;
            }
            else
            {
                return Store.GetStateRootHash(rawBlock.Header.PreviousHash) is { } prevStateRootHash
                    ? prevStateRootHash
                    : StateStore.GetStateRoot(default).Hash;
            }
        }
        finally
        {
            _rwlock.ExitWriteLock();
        }
    }

    internal CommittedActionEvaluation[] EvaluateBlockPrecededStateRootHash(RawBlock rawBlock)
        => ActionEvaluator.Evaluate(rawBlock, Store.GetStateRootHash(rawBlock.Header.PreviousHash));
}
