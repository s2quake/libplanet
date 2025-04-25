#nullable disable
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain
{
    public partial class BlockChain
    {
        /// <summary>
        /// Determines the state root hash given <paramref name="block"/> and
        /// <paramref name="evaluations"/>.
        /// </summary>
        /// <param name="block">The <see cref="RawBlock"/> to execute for
        /// <paramref name="evaluations"/>.</param>
        /// <param name="evaluations">The list of <see cref="IActionEvaluation"/>s
        /// from which to extract the states to commit.</param>
        /// <exception cref="InvalidActionException">Thrown when given <paramref name="block"/>
        /// contains an action that cannot be loaded with <see cref="IActionLoader"/>.</exception>
        /// <returns>The state root hash given <paramref name="block"/> and
        /// its <paramref name="evaluations"/>.
        /// </returns>
        /// <remarks>
        /// Since the state root hash can only be calculated by making a commit
        /// to an <see cref="IStateStore"/>, this always has a side-effect to
        /// <see cref="StateStore"/> regardless of whether the state root hash
        /// obdatined through committing to <see cref="StateStore"/>
        /// matches the <paramref name="block"/>'s <see cref="Block.StateRootHash"/> or not.
        /// </remarks>
        /// <seealso cref="EvaluateBlockPrecededStateRootHash"/>
        /// <seealso cref="ValidateBlockPrecededStateRootHash"/>
        public HashDigest<SHA256> DetermineNextBlockStateRootHash(
            Block block, out IReadOnlyList<ICommittedActionEvaluation> evaluations)
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
                    block.Index,
                    block.Hash,
                    evaluations.Count);

                if (evaluations.Count > 0)
                {
                    return evaluations.Last().OutputState;
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

        /// <summary>
        /// Evaluates the <see cref="IAction"/>s in given <paramref name="block"/>.
        /// </summary>
        /// <param name="block">The <see cref="RawBlock"/> to execute.</param>
        /// <returns>An <see cref="IReadOnlyList{T}"/> of <ses cref="ICommittedActionEvaluation"/>s
        /// for given <paramref name="block"/>.</returns>
        /// <exception cref="InvalidActionException">Thrown when given <paramref name="block"/>
        /// contains an action that cannot be loaded with <see cref="IActionLoader"/>.</exception>
        /// <seealso cref="ValidateBlockPrecededStateRootHash"/>
        [Pure]
        public IReadOnlyList<ICommittedActionEvaluation> EvaluateBlock(Block block) =>
            block.ProtocolVersion >= BlockMetadata.SlothProtocolVersion
                ? ActionEvaluator.Evaluate(
                    block.RawBlock,
                    Store.GetStateRootHash(block.Hash))
                : ActionEvaluator.Evaluate(
                    block.RawBlock,
                    Store.GetStateRootHash(block.PreviousHash));

        /// <summary>
        /// Evaluates all actions in the <see cref="RawBlock.Transactions"/> and
        /// optional <see cref="IAction"/>s in
        /// <see cref="Policies.IBlockPolicy.PolicyActionsRegistry"/> and returns
        /// a <see cref="Block"/> instance combined with the <see cref="Block.StateRootHash"/>
        /// The returned <see cref="Block"/> is signed by the given <paramref name="privateKey"/>.
        /// </summary>
        /// <param name="rawBlock">The <see cref="RawBlock"/> to evaluate
        /// and sign.</param>
        /// <param name="privateKey">The private key to be used for signing the block.
        /// This must match to the block's <see cref="RawBlockHeader.Miner"/> and
        /// <see cref="RawBlockHeader.PublicKey"/>.</param>
        /// <returns>The block combined with the resulting <see cref="Block.StateRootHash"/>.
        /// It is signed by the given <paramref name="privateKey"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when the block's
        /// <see cref="RawBlockHeader.ProtocolVersion"/> is less than 2.</exception>
        /// <exception cref="ArgumentException">Thrown when the given <paramref name="privateKey"/>
        /// does not match to the block miner's <see cref="PublicKey"/>.</exception>
        // FIXME: Remove this method.
        internal Block EvaluateAndSign(
            RawBlock rawBlock, PrivateKey privateKey)
        {
            if (rawBlock.ProtocolVersion < BlockMetadata.SlothProtocolVersion)
            {
                if (rawBlock.ProtocolVersion < BlockMetadata.SignatureProtocolVersion)
                {
                    throw new ArgumentException(
                        $"Given {nameof(rawBlock)} must have protocol version " +
                        $"2 or greater: {rawBlock.ProtocolVersion}");
                }
                else
                {
                    return rawBlock.Sign(
                        privateKey,
                        DetermineBlockPrecededStateRootHash(rawBlock, out _));
                }
            }
            else
            {
                if (rawBlock.Index < 1)
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
        }

        /// <summary>
        /// Determines the state root hash given <paramref name="block"/> and
        /// <paramref name="evaluations"/>.
        /// </summary>
        /// <param name="block">The <see cref="RawBlock"/> to execute for
        /// <paramref name="evaluations"/>.</param>
        /// <param name="evaluations">The list of <see cref="IActionEvaluation"/>s
        /// from which to extract the states to commit.</param>
        /// <exception cref="InvalidActionException">Thrown when given <paramref name="block"/>
        /// contains an action that cannot be loaded with <see cref="IActionLoader"/>.</exception>
        /// <returns>The state root hash given <paramref name="block"/> and
        /// its <paramref name="evaluations"/>.
        /// </returns>
        /// <remarks>
        /// Since the state root hash can only be calculated by making a commit
        /// to an <see cref="IStateStore"/>, this always has a side-effect to
        /// <see cref="StateStore"/> regardless of whether the state root hash
        /// obdatined through committing to <see cref="StateStore"/>
        /// matches the <paramref name="block"/>'s <see cref="Block.StateRootHash"/> or not.
        /// </remarks>
        /// <seealso cref="EvaluateBlockPrecededStateRootHash"/>
        /// <seealso cref="ValidateBlockPrecededStateRootHash"/>
        internal HashDigest<SHA256> DetermineBlockPrecededStateRootHash(
            RawBlock block, out IReadOnlyList<ICommittedActionEvaluation> evaluations)
        {
            _rwlock.EnterWriteLock();
            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                evaluations = EvaluateBlockPrecededStateRootHash(block);

                _logger.Debug(
                    "Took {DurationMs} ms to evaluate block #{BlockHeight} " +
                    "pre-evaluation hash {RawHash} with {Count} action evaluations",
                    stopwatch.ElapsedMilliseconds,
                    block.Index,
                    block.RawHash,
                    evaluations.Count);

                if (evaluations.Count > 0)
                {
                    return evaluations.Last().OutputState;
                }
                else
                {
                    return Store.GetStateRootHash(block.PreviousHash) is { } prevStateRootHash
                        ? prevStateRootHash
                        : StateStore.GetStateRoot(default).Hash;
                }
            }
            finally
            {
                _rwlock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Evaluates the <see cref="IAction"/>s in given <paramref name="rawBlock"/>.
        /// </summary>
        /// <param name="rawBlock">The <see cref="RawBlock"/> to execute.
        /// </param>
        /// <returns>An <see cref="IReadOnlyList{T}"/> of <ses cref="ICommittedActionEvaluation"/>s
        /// for given <paramref name="rawBlock"/>.</returns>
        /// <exception cref="InvalidActionException">Thrown when given
        /// <paramref name="rawBlock"/>
        /// contains an action that cannot be loaded with <see cref="IActionLoader"/>.</exception>
        /// <seealso cref="ValidateBlockPrecededStateRootHash"/>
        [Pure]
        internal IReadOnlyList<ICommittedActionEvaluation> EvaluateBlockPrecededStateRootHash(
            RawBlock rawBlock) =>
            ActionEvaluator.Evaluate(
                rawBlock,
                Store.GetStateRootHash(rawBlock.PreviousHash));
    }
}
