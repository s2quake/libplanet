using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using Libplanet.Types.Crypto;
using Libplanet.Types.Tx;
using Serilog;

namespace Libplanet.Blockchain.Policies
{
    /// <summary>
    /// <para>
    /// An in memory implementation of the <see cref="IStagePolicy"/>.
    /// </para>
    /// <para>
    /// This implementation holds on to every unconfirmed <see cref="Transaction"/> except
    /// for the following reasons:
    /// <list type="bullet">
    ///     <item>
    ///         <description>A <see cref="Transaction"/> has been specifically marked to
    ///         be ignored due to <see cref="Transaction"/> not being valid.</description>
    ///     </item>
    ///     <item>
    ///         <description>A <see cref="Transaction"/> has expired due to its staleness.
    ///         </description>
    ///     </item>
    /// </list>
    /// </para>
    /// <para>
    /// Additionally, any <see cref="Transaction"/> with a lower nonce than that of returned by
    /// the <see cref="BlockChain"/> is masked and filtered by default.
    /// </para>
    /// </summary>
    public class VolatileStagePolicy : IStagePolicy
    {
        private readonly ConcurrentDictionary<TxId, Transaction> _staged;
        private readonly HashSet<TxId> _ignored;
        private readonly ReaderWriterLockSlim _lock;
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a new <see cref="VolatileStagePolicy"/> instance.
        /// By default, <see cref="Lifetime"/> is set to 10 minutes.
        /// </summary>
        public VolatileStagePolicy()
            : this(TimeSpan.FromSeconds(10 * 60))
        {
        }

        /// <summary>
        /// Creates a new <see cref="VolatileStagePolicy"/> instance.
        /// </summary>
        /// <param name="lifetime">Volatilizes staged transactions older than this
        /// <see cref="TimeSpan"/>.  See also <see cref="Lifetime"/>.</param>
        public VolatileStagePolicy(TimeSpan lifetime)
        {
            _logger = Log
                .ForContext<VolatileStagePolicy>()
                .ForContext("Source", nameof(VolatileStagePolicy));
            Lifetime = lifetime;
            _staged = new ConcurrentDictionary<TxId, Transaction>();
            _ignored = new HashSet<TxId>();
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }

        /// <summary>
        /// Lifespan for <see cref="Transaction"/>s.  Any <see cref="Transaction"/> older
        /// than this <see cref="TimeSpan"/> will be considered expired.
        /// </summary>
        /// <remarks>
        /// Expired <see cref="Transaction"/>s cannot be staged.
        /// </remarks>
        public TimeSpan Lifetime { get; }

        public bool Stage(BlockChain blockChain, Transaction transaction)
        {
            if (Expired(transaction))
            {
                return false;
            }

            bool result;
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_ignored.Contains(transaction.Id))
                {
                    return false;
                }
                else
                {
                    _lock.EnterWriteLock();
                    try
                    {
                        result = _staged.TryAdd(transaction.Id, transaction);
                        if (result)
                        {
                            const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";
                            _logger
                                .ForContext("Tag", "Metric")
                                .ForContext("Subtag", "TxStageTimestamp")
                                .Information(
                                    "Transaction {TxId} by {Signer} " +
                                    "with timestamp {TxTimestamp} staged at {StagedTimestamp}",
                                    transaction.Id,
                                    transaction.Signer,
                                    transaction.Timestamp.ToString(
                                        TimestampFormat, CultureInfo.InvariantCulture),
                                    DateTimeOffset.UtcNow.ToString(
                                        TimestampFormat, CultureInfo.InvariantCulture));
                            _logger
                                .ForContext("Tag", "Metric")
                                .ForContext("Subtag", "StagedCount")
                                .Information(
                                    "There are {Count} transactions staged",
                                    _staged.Count);
                        }
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }

            return result;
        }

        public bool Unstage(BlockChain blockChain, TxId id)
        {
            bool result;
            _lock.EnterWriteLock();
            try
            {
                result = _staged.TryRemove(id, out _);
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            return result;
        }

        public void Ignore(BlockChain blockChain, TxId id)
        {
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_ignored.Contains(id))
                {
                    return;
                }

                _lock.EnterWriteLock();
                try
                {
                    _ignored.Add(id);
                    _logger.Information(
                        "Transaction {TxId} is marked as ignored",
                        id);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        public bool Ignores(BlockChain blockChain, TxId id)
        {
            _lock.EnterReadLock();
            try
            {
                return _ignored.Contains(id);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public Transaction? Get(BlockChain blockChain, TxId id, bool filtered = true)
        {
            _lock.EnterReadLock();
            try
            {
                return GetInner(blockChain, id, filtered);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public IEnumerable<Transaction> Iterate(BlockChain blockChain, bool filtered = true)
        {
            List<Transaction> transactions = new List<Transaction>();

            _lock.EnterReadLock();
            try
            {
                List<TxId> txIds = _staged.Keys.ToList();
                foreach (TxId txId in txIds)
                {
                    if (GetInner(blockChain, txId, filtered) is Transaction tx)
                    {
                        transactions.Add(tx);
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            return transactions;
        }

        public long GetNextTxNonce(BlockChain blockChain, Address address)
        {
            long nonce = blockChain.Store.GetTxNonce(blockChain.Id, address);
            IEnumerable<Transaction> orderedTxs = Iterate(blockChain, filtered: true)
                .Where(tx => tx.Signer.Equals(address))
                .OrderBy(tx => tx.Nonce);

            foreach (Transaction tx in orderedTxs)
            {
                if (nonce < tx.Nonce)
                {
                    break;
                }
                else if (nonce == tx.Nonce)
                {
                    nonce++;
                }
            }

            return nonce;
        }

        private bool Expired(Transaction transaction) =>
            Lifetime < DateTimeOffset.UtcNow - transaction.Timestamp;

        /// <remarks>
        /// It has been intended to avoid recursive lock, hence doesn't hold any synchronous scope.
        /// Therefore, we should manage the lock from its caller side.
        /// </remarks>
        private Transaction? GetInner(BlockChain blockChain, TxId id, bool filtered)
        {
            if (_staged.TryGetValue(id, out Transaction? tx) && tx is { })
            {
                if (Expired(tx) || _ignored.Contains(tx.Id))
                {
                    _staged.TryRemove(id, out _);
                    return null;
                }
                else if (filtered)
                {
                    return blockChain.Store.GetTxNonce(blockChain.Id, tx.Signer) <= tx.Nonce
                        ? tx
                        : null;
                }
                else
                {
                    return tx;
                }
            }
            else
            {
                return null;
            }
        }
    }
}
