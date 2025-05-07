using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Blockchain.Policies;
using Libplanet.Serialization;
using Libplanet.Types;
using Libplanet.Types.Blocks;
using Libplanet.Types.Crypto;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace Libplanet.Blockchain;

public partial class BlockChain
{
    public static Block ProposeGenesisBlock(
        PrivateKey privateKey,
        HashDigest<SHA256>? stateRootHash = null,
        ImmutableSortedSet<Transaction>? transactions = null,
        DateTimeOffset? timestamp = null)
    {
        transactions ??= [];

        var metadata = new BlockHeader
        {
            Height = 0L,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            Proposer = privateKey.Address,
            PreviousHash = default,
        };
        var content = new BlockContent
        {
            Transactions = transactions,
            Evidences = [],
        };

        RawBlock preEval = RawBlock.Propose(metadata, content);
        stateRootHash ??= default;
        return preEval.Sign(privateKey, (HashDigest<SHA256>)stateRootHash);
    }

    public Block ProposeBlock(
        PrivateKey proposer,
        BlockCommit? lastCommit = null,
        ImmutableSortedSet<EvidenceBase>? evidence = null,
        IComparer<Transaction>? txPriority = null)
    {
        var height = Count;
        _logger.Debug("Starting to propose block #{Height}...", height);

        ImmutableArray<Transaction> transactions;
        try
        {
            transactions = GatherTransactionsToPropose(height, txPriority);
        }
        catch (InvalidOperationException ioe)
        {
            throw new OperationCanceledException(
                $"Failed to gather transactions to propose for block #{height}.",
                ioe);
        }

        var block = ProposeBlock(
            proposer,
            transactions.ToImmutableSortedSet(),
            lastCommit ?? BlockCommit.Empty,
            evidence ?? []);
        _logger.Debug(
            "Proposed block #{Height} {Hash} with previous hash {PreviousHash}",
            block.Height,
            block.BlockHash,
            block.PreviousHash);

        return block;
    }

    /// <summary>
    /// <para>
    /// Proposes a next <see cref="Block"/> using a specified
    /// list of <see cref="Transaction"/>s.
    /// </para>
    /// <para>
    /// Unlike <see cref="ProposeBlock(PrivateKey, BlockCommit, ImmutableArray{EvidenceBase}?,
    /// IComparer{Transaction})"/>,
    /// this may result in a <see cref="Block"/> that does not conform to the
    /// <see cref="Policy"/>.
    /// </para>
    /// </summary>
    /// <param name="proposer">The proposer's <see cref="PublicKey"/> that proposes the block.
    /// </param>
    /// <param name="transactions">The list of <see cref="Transaction"/>s to include.</param>
    /// <param name="lastCommit">The <see cref="BlockCommit"/> evidence of the previous
    /// <see cref="Block"/>.</param>
    /// <param name="evidence">The <see cref="EvidenceBase"/>s to be committed.</param>
    /// <returns>A <see cref="Block"/> that is proposed.</returns>
    internal Block ProposeBlock(
        PrivateKey proposer,
        ImmutableSortedSet<Transaction> transactions,
        BlockCommit lastCommit,
        ImmutableSortedSet<EvidenceBase> evidence)
    {
        long index = Count;
        BlockHash prevHash = Store.IndexBlockHash(Id, index - 1)
            ?? throw new NullReferenceException($"Chain {Id} is missing block #{index - 1}");

        HashDigest<SHA256> stateRootHash = GetNextStateRootHash(prevHash) ??
            throw new InvalidOperationException(
                $"Cannot propose a block as the next state root hash " +
                $"for block {prevHash} is missing.");

        // FIXME: Should use automated public constructor.
        // Manual internal constructor is used purely for testing custom timestamps.
        var metadata = new BlockHeader
        {
            ProtocolVersion = BlockHeader.CurrentProtocolVersion,
            Height = index,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            PreviousHash = prevHash,
            LastCommit = lastCommit,
        };
        var blockContent = new BlockContent
        {
            Transactions = transactions,
            Evidences = evidence,
        };
        var preEval = RawBlock.Propose(metadata, blockContent);
        return ProposeBlock(proposer, preEval, stateRootHash);
    }

    internal Block ProposeBlock(
        PrivateKey proposer,
        RawBlock rawBlock,
        HashDigest<SHA256> stateRootHash) =>
        rawBlock.Sign(proposer, stateRootHash);

    /// <summary>
    /// Gathers <see cref="Transaction"/>s for proposing a <see cref="Block"/> for
    /// index <pararef name="index"/>.  Gathered <see cref="Transaction"/>s are
    /// guaranteed to satisfied the following <see cref="Transaction"/> related
    /// policies:
    /// <list type="bullet">
    ///     <item><description>
    ///         <see cref="BlockPolicy.GetMaxTransactionsBytes"/>
    ///     </description></item>
    ///     <item><description>
    ///         <see cref="BlockPolicy.GetMaxTransactionsPerBlock"/>
    ///     </description></item>
    ///     <item><description>
    ///         <see cref="BlockPolicy.GetMaxTransactionsPerSignerPerBlock"/>
    ///     </description></item>
    ///     <item><description>
    ///         <see cref="BlockPolicy.GetMinTransactionsPerBlock"/>
    ///     </description></item>
    /// </list>
    /// </summary>
    /// <param name="index">The index of the <see cref="Block"/> to propose.</param>
    /// <param name="txPriority">An optional comparer for give certain transactions to
    /// priority to belong to the block.  No certain priority by default.</param>
    /// <returns>An <see cref="ImmutableList"/> of <see cref="Transaction"/>s
    /// to propose.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not all policies
    /// can be satisfied.</exception>
    internal ImmutableArray<Transaction> GatherTransactionsToPropose(
        long index,
        IComparer<Transaction>? txPriority = null) =>
        GatherTransactionsToPropose(
            Policy.GetMaxTransactionsBytes(index),
            Policy.GetMaxTransactionsPerBlock(index),
            Policy.GetMaxTransactionsPerSignerPerBlock(index),
            Policy.GetMinTransactionsPerBlock(index),
            txPriority);

    /// <summary>
    /// Gathers <see cref="Transaction"/>s for proposing a next block
    /// from the current set of staged <see cref="Transaction"/>s.
    /// </summary>
    /// <param name="maxTransactionsBytes">The maximum number of bytes a block can have.</param>
    /// <param name="maxTransactions">The maximum number of <see cref="Transaction"/>s
    /// allowed.</param>
    /// <param name="maxTransactionsPerSigner">The maximum number of
    /// <see cref="Transaction"/>s with the same signer allowed.</param>
    /// <param name="minTransactions">The minimum number of <see cref="Transaction"/>s
    /// allowed.</param>
    /// <param name="txPriority">An optional comparer for give certain transactions to
    /// priority to belong to the block.  No certain priority by default.</param>
    /// <returns>An <see cref="ImmutableList"/> of <see cref="Transaction"/>s with its
    /// count not exceeding <paramref name="maxTransactions"/> and the number of
    /// <see cref="Transaction"/>s in the list for each signer not exceeding
    /// <paramref name="maxTransactionsPerSigner"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not all policies
    /// can be satisfied.</exception>
    internal ImmutableArray<Transaction> GatherTransactionsToPropose(
        long maxTransactionsBytes,
        int maxTransactions,
        int maxTransactionsPerSigner,
        int minTransactions,
        IComparer<Transaction>? txPriority = null)
    {
        long index = Count;
        ImmutableList<Transaction> stagedTransactions = ListStagedTransactions(txPriority);
        _logger.Information(
            "Gathering transactions to propose for block #{Index} from {TxCount} " +
            "staged transactions...",
            index,
            stagedTransactions.Count);

        var transactions = new List<Transaction>();

        // FIXME: The tx collection timeout should be configurable.
        DateTimeOffset timeout = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(4);

        var estimatedEncoding = 0L;
        var storedNonces = new Dictionary<Address, long>();
        var nextNonces = new Dictionary<Address, long>();
        var toProposeCounts = new Dictionary<Address, int>();

        foreach (
            (Transaction tx, int i) in stagedTransactions.Select((val, idx) => (val, idx)))
        {
            _logger.Verbose(
                "Validating tx {Iter}/{Total} {TxId} to include in block #{Index}...",
                i,
                stagedTransactions.Count,
                tx.Id,
                index);

            // We don't care about nonce ordering here because `.ListStagedTransactions()`
            // returns already ordered transactions by its nonce.
            if (!storedNonces.ContainsKey(tx.Signer))
            {
                storedNonces[tx.Signer] = Store.GetTxNonce(Id, tx.Signer);
                nextNonces[tx.Signer] = storedNonces[tx.Signer];
                toProposeCounts[tx.Signer] = 0;
            }

            if (transactions.Count >= maxTransactions)
            {
                _logger.Information(
                    "Ignoring tx {Iter}/{Total} {TxId} and the rest of the " +
                    "staged transactions due to the maximum number of " +
                    "transactions per block allowed has been reached: {Max}",
                    i,
                    stagedTransactions.Count,
                    tx.Id,
                    maxTransactions);
                break;
            }

            if (storedNonces[tx.Signer] <= tx.Nonce && tx.Nonce == nextNonces[tx.Signer])
            {
                if (Policy.ValidateNextBlockTx(this, tx) is { } tpve)
                {
                    _logger.Debug(
                        "Ignoring tx {Iter}/{Total} {TxId} as it does not follow policy",
                        i,
                        stagedTransactions.Count,
                        tx.Id);
                    StagePolicy.Ignore(this, tx.Id);
                    continue;
                }

                var txAddedEncoding = estimatedEncoding + ModelSerializer.SerializeToBytes(tx).Length;
                if (txAddedEncoding > maxTransactionsBytes)
                {
                    _logger.Debug(
                        "Ignoring tx {Iter}/{Total} {TxId} due to the maximum size allowed " +
                        "for transactions in a block: {CurrentEstimate}/{MaximumBlockBytes}",
                        i,
                        stagedTransactions.Count,
                        tx.Id,
                        txAddedEncoding,
                        maxTransactionsBytes);
                    continue;
                }
                else if (toProposeCounts[tx.Signer] >= maxTransactionsPerSigner)
                {
                    _logger.Debug(
                        "Ignoring tx {Iter}/{Total} {TxId} due to the maximum number " +
                        "of transactions allowed per single signer per block " +
                        "has been reached: {Max}",
                        i,
                        stagedTransactions.Count,
                        tx.Id,
                        maxTransactionsPerSigner);
                    continue;
                }

                try
                {
                    _ = tx.Actions.Select(item => item.ToAction<IAction>());
                }
                catch (Exception e)
                {
                    _logger.Error(
                        e,
                        "Failed to load an action in tx; marking tx {TxId} as ignored...",
                        tx.Id);
                    StagePolicy.Ignore(this, tx.Id);
                    continue;
                }

                _logger.Verbose(
                    "Adding tx {Iter}/{Total} {TxId} to the list of transactions " +
                    "to be proposed",
                    i,
                    stagedTransactions.Count,
                    tx.Id);
                transactions.Add(tx);
                nextNonces[tx.Signer] += 1;
                toProposeCounts[tx.Signer] += 1;
                estimatedEncoding = txAddedEncoding;
            }
            else if (tx.Nonce < storedNonces[tx.Signer])
            {
                _logger.Debug(
                    "Ignoring tx {Iter}/{Total} {TxId} by {Signer} " +
                    "as it has lower nonce {Actual} than expected nonce {Expected}",
                    i,
                    stagedTransactions.Count,
                    tx.Id,
                    tx.Signer,
                    tx.Nonce,
                    nextNonces[tx.Signer]);
            }
            else
            {
                _logger.Debug(
                    "Ignoring tx {Iter}/{Total} {TxId} by {Signer} " +
                    "as it has higher nonce {Actual} than expected nonce {Expected}",
                    i,
                    stagedTransactions.Count,
                    tx.Id,
                    tx.Signer,
                    tx.Nonce,
                    nextNonces[tx.Signer]);
            }

            if (timeout < DateTimeOffset.UtcNow)
            {
                _logger.Debug(
                    "Reached the time limit to collect staged transactions; other staged " +
                    "transactions will be proposed later");
                break;
            }
        }

        if (transactions.Count < minTransactions)
        {
            throw new InvalidOperationException(
                $"Only gathered {transactions.Count} transactions where " +
                $"the minimal number of transactions to propose is {minTransactions}.");
        }

        _logger.Information(
            "Gathered total of {TransactionsCount} transactions to propose for " +
            "block #{Index} from {StagedTransactionsCount} staged transactions",
            transactions.Count,
            index,
            stagedTransactions.Count);
        return transactions.ToImmutableArray();
    }
}
