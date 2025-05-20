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
    public HashDigest<SHA256> DetermineNextBlockStateRootHash(Block block, out ActionEvaluation[] evaluations)
    {
        evaluations = EvaluateBlock(block);

        if (evaluations.Length > 0)
        {
            return evaluations[^1].OutputWorld.Trie.Hash;
        }

        return block.StateRootHash;
    }

    public ActionEvaluation[] EvaluateBlock(Block block)
        => _actionEvaluator.Evaluate((RawBlock)block, block.StateRootHash);

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
        RawBlock rawBlock, out ActionEvaluation[] evaluations)
    {
        // _rwlock.EnterWriteLock();
        try
        {
            evaluations = EvaluateBlockPrecededStateRootHash(rawBlock);

            if (evaluations.Length > 0)
            {
                return evaluations[^1].OutputWorld.Trie.Hash;
            }

            return _repository.BlockDigests[rawBlock.Header.PreviousHash].StateRootHash;
        }
        finally
        {
            // _rwlock.ExitWriteLock();
        }
    }

    internal ActionEvaluation[] EvaluateBlockPrecededStateRootHash(RawBlock rawBlock)
        => _actionEvaluator.Evaluate(rawBlock, _repository.BlockDigests[rawBlock.Header.PreviousHash].StateRootHash);
}
