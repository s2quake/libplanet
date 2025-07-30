using System.Diagnostics.Contracts;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Action
{
    /// <summary>
    /// Contextual data determined by a transaction and a block.
    /// Passed to <see cref="IAction.Execute(IActionContext)"/> method.
    /// </summary>
    public interface IActionContext
    {
        /// <summary>
        /// The <see cref="Transaction.Signer"/> of the <see cref="Transaction"/> that contains
        /// the <see cref="IAction"/> to be executed.  If the <see cref="IAction"/> is
        /// not part of a <see cref="Transaction"/>, e.g. <see cref="IBlockPolicy.BlockAction"/>,
        /// this is set to <see cref="Block.Miner"/> instead.
        /// </summary>
        [Pure]
        Address Signer { get; }

        /// <summary>
        /// The <see cref="Transaction.Id"/> of the <see cref="Transaction"/> that contains
        /// the <see cref="IAction"/>.  If the <see cref="IAction"/> is not part of
        /// a <see cref="Transaction"/>, e.g. <see cref="IBlockPolicy.BlockAction"/>,
        /// this is set to <see langword="null"/>.
        /// </summary>
        [Pure]
        TxId? TxId { get; }

        /// <summary>
        /// The <see cref="Block.Miner"/> of the <see cref="Block"/> that contains
        /// the <see cref="IAction"/>.
        /// </summary>
        [Pure]
        Address Miner { get; }

        /// <summary>
        /// The <see cref="Block.Index"/> of the <see cref="Block"/> that contains
        /// the <see cref="IAction"/>.
        /// </summary>
        [Pure]
        long BlockHeight { get; }

        /// <summary>
        /// The <see cref="Block.ProtocolVersion"/> of the <see cref="Block"/> that contains
        /// the <see cref="IAction"/>.
        /// </summary>
        [Pure]
        int BlockProtocolVersion { get; }

        /// <summary>
        /// The <see cref="BlockCommit"/> about previous <see cref="Block"/>'s vote information.
        /// <see langword="null"/> if the block is the genesis block.
        /// </summary>
        [Pure]
        BlockCommit? LastCommit { get; }

        /// <summary>
        /// A null delta of states, which means it represents the states
        /// before <see cref="IAction"/> executes.
        /// <para>Although a <see cref="IAccount"/> instance is
        /// immutable, it has several manipulative methods that returns
        /// new <see cref="IAccount"/> instances with some "dirty"
        /// states.  These kinds of dirty <see cref="IWorld"/>
        /// instances can be returned by <see
        /// cref="IAction.Execute(IActionContext)"/> method.</para>
        /// </summary>
        [Pure]
        IWorld PreviousState { get; }

        /// <summary>
        /// The random seed to use for pseudorandom number generator.  This value
        /// is determined by various block properties, the signature of the transaction
        /// containing the action to execute, and index of the action to execute, which is
        /// deterministic so that every node can replay the same action and
        /// then reproduce the same result, while neither a single block miner
        /// nor a single transaction signer can predict the result and cheat.
        /// </summary>
        /// <seealso cref="GetRandom"/>
        int RandomSeed { get; }

        /// <summary>
        /// Whether this action is executed as a policy action.
        /// <see langword="false"/> if it is a user action.
        /// </summary>
        [Pure]
        bool IsPolicyAction { get; }

        /// <summary>
        /// Max gas price set by the transaction.
        /// <see langword="null"/> if the action does not belongs to a transaction.
        /// </summary>
        [Pure]
        FungibleAssetValue? MaxGasPrice { get; }

        /// <summary>
        /// A list of <see cref="Transaction"/>s that are included in a <see cref="Block"/> as
        /// the <see cref="IAction"/> to be evaluated.  This information is provided only if
        /// <see cref="IsPolicyAction"/> is <see langword="true"/>, otherwise returns an empty set.
        /// </summary>
        [Pure]
        IReadOnlyList<Transaction> Txs { get; }

        /// <summary>
        /// A list of <see cref="EvidenceBase"/>s that are included in a
        /// <see cref="Block"/>.
        /// </summary>
        [Pure]
        IReadOnlyList<EvidenceBase> Evidence { get; }

        /// <summary>
        /// Returns a newly initialized <see cref="IRandom"/> using <see cref="RandomSeed"/>
        /// as its seed value.
        /// </summary>
        /// <returns>A newly initialized <see cref="IRandom"/> using <see cref="RandomSeed"/>
        /// as its seed value.</returns>
        [Pure]
        IRandom GetRandom();
    }
}
