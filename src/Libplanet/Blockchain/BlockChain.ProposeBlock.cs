using System.Security.Cryptography;
using Libplanet.Action;
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
        PrivateKey proposer,
        HashDigest<SHA256>? stateRootHash = null,
        ImmutableSortedSet<Transaction>? transactions = null,
        DateTimeOffset? timestamp = null)
    {
        var header = new BlockHeader
        {
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
        };
        var content = new BlockContent
        {
            Transactions = transactions ?? [],
            Evidences = [],
        };

        var rawBlock = RawBlock.Create(header, content);
        return rawBlock.Sign(proposer, stateRootHash ?? default);
    }

    public Block ProposeBlock(
        PrivateKey proposer,
        BlockCommit? lastCommit = null,
        ImmutableSortedSet<EvidenceBase>? evidences = null,
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
            lastCommit ?? BlockCommit.Empty,
            [.. transactions],
            evidences ?? []);
        _logger.Debug(
            "Proposed block #{Height} {Hash} with previous hash {PreviousHash}",
            block.Height,
            block.BlockHash,
            block.PreviousHash);

        return block;
    }

    internal Block ProposeBlock(
        PrivateKey proposer,
        BlockCommit lastCommit,
        ImmutableSortedSet<Transaction> transactions,
        ImmutableSortedSet<EvidenceBase> evidences)
    {
        var height = Count;
        var previousHash = Store.GetBlockHash(Id, height - 1);

        HashDigest<SHA256> stateRootHash = GetNextStateRootHash(previousHash) ??
            throw new InvalidOperationException(
                $"Cannot propose a block as the next state root hash " +
                $"for block {previousHash} is missing.");

        // FIXME: Should use automated public constructor.
        // Manual internal constructor is used purely for testing custom timestamps.
        var metadata = new BlockHeader
        {
            Height = height,
            Timestamp = DateTimeOffset.UtcNow,
            Proposer = proposer.Address,
            PreviousHash = previousHash,
            LastCommit = lastCommit,
        };
        var blockContent = new BlockContent
        {
            Transactions = transactions,
            Evidences = evidences,
        };

        var rawBlock = RawBlock.Create(metadata, blockContent);
        return rawBlock.Sign(proposer, stateRootHash);
    }

    /// <summary>
    /// Gathers <see cref="Transaction"/>s for proposing a <see cref="Block"/> for
    /// index <pararef name="index"/>.  Gathered <see cref="Transaction"/>s are
    /// guaranteed to satisfied the following <see cref="Transaction"/> related
    /// policies:
    /// <list type="bullet">
    ///     <item><description>
    ///         <see cref="BlockChainOptions.MaxTransactionsBytes"/>
    ///     </description></item>
    ///     <item><description>
    ///         <see cref="BlockChainOptions.MaxTransactionsPerBlock"/>
    ///     </description></item>
    ///     <item><description>
    ///         <see cref="BlockChainOptions.MaxTransactionsPerSignerPerBlock"/>
    ///     </description></item>
    ///     <item><description>
    ///         <see cref="BlockChainOptions.MinTransactionsPerBlock"/>
    ///     </description></item>
    /// </list>
    /// </summary>
    /// <param name="height">The index of the <see cref="Block"/> to propose.</param>
    /// <param name="txPriority">An optional comparer for give certain transactions to
    /// priority to belong to the block.  No certain priority by default.</param>
    /// <returns>An <see cref="ImmutableList"/> of <see cref="Transaction"/>s
    /// to propose.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not all policies
    /// can be satisfied.</exception>
    internal ImmutableArray<Transaction> GatherTransactionsToPropose(
        int height, IComparer<Transaction>? txPriority = null)
        => GatherTransactionsToPropose(
            Options.MaxTransactionsBytes,
            Options.MaxTransactionsPerBlock,
            Options.MaxTransactionsPerSignerPerBlock,
            Options.MinTransactionsPerBlock,
            txPriority);

    internal ImmutableArray<Transaction> GatherTransactionsToPropose(
        long maxTransactionsBytes,
        int maxTransactions,
        int maxTransactionsPerSigner,
        int minTransactions,
        IComparer<Transaction>? txPriority = null)
    {
        var index = Count;
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
                try
                {
                    Options.ValidateTransaction(this, tx);
                }
                catch
                {
                    StagedTransactions.Ignore(tx.Id);
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
                    StagedTransactions.Ignore(tx.Id);
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
