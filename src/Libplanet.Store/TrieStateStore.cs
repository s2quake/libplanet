using System.Diagnostics;
using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Store.Trie.Nodes;
using Serilog;

namespace Libplanet.Store;

/// <summary>
/// An <see cref="IStateStore"/> implementation. It stores states with <see cref="Trie.Trie"/>.
/// </summary>
/// <remarks>
/// Creates a new <see cref="TrieStateStore"/>.
/// </remarks>
/// <param name="keyValueStore">The storage to store states. It used by
/// <see cref="Trie.Trie"/> in internal.</param>
public partial class TrieStateStore(IKeyValueStore keyValueStore) : IStateStore
{
    private readonly ILogger _logger = Log.ForContext<TrieStateStore>();
    private readonly HashNodeCache _cache = new();
    private bool _disposed = false;

    public TrieStateStore()
        : this(new MemoryKeyValueStore())
    {
    }

    public IKeyValueStore StateKeyValueStore { get; } = keyValueStore;

    /// <summary>
    /// <para>
    /// Copies states under state root hashes of given <paramref name="stateRootHashes"/>
    /// to <paramref name="targetStateStore"/>.
    /// </para>
    /// <para>
    /// Under the hood, this not only copies states directly associated
    /// with <paramref name="stateRootHashes"/>, but also automatically copies states
    /// that are not directly associated with <paramref name="stateRootHashes"/>
    /// but associated with "subtries" with references stored in <see cref="ITrie"/>s
    /// of <paramref name="stateRootHashes"/>.
    /// </para>
    /// </summary>
    /// <param name="stateRootHashes">The state root hashes of states to copy.</param>
    /// <param name="targetStateStore">The target state store to copy state root hashes.</param>
    /// <exception cref="ArgumentException">Thrown when a state root cannot be found for
    /// any of given <paramref name="stateRootHashes"/>.</exception>
    public void CopyStates(
        IImmutableSet<HashDigest<SHA256>> stateRootHashes, TrieStateStore targetStateStore)
    {
        IKeyValueStore targetKeyValueStore = targetStateStore.StateKeyValueStore;
        var stopwatch = new Stopwatch();
        long count = 0;
        _logger.Verbose("Started {MethodName}()", nameof(CopyStates));
        stopwatch.Start();

        foreach (HashDigest<SHA256> stateRootHash in stateRootHashes)
        {
            var stateTrie = (Trie.Trie)GetStateRoot(stateRootHash);
            if (!stateTrie.IsCommitted)
            {
                throw new ArgumentException(
                    $"Failed to find a state root for given state root hash {stateRootHash}.");
            }

            foreach (var (key, value) in stateTrie)
            {
                targetKeyValueStore[key] = StateKeyValueStore[key];
                count++;
            }

            // FIXME: Probably not the right place to implement this.
            // It'd be better to have it in Libplanet.Action.State.
            if (stateTrie[KeyBytes.Empty] is { } metadata)
            {
                foreach (var (path, hash) in stateTrie)
                {
                    // Ignore metadata
                    if (path.Length > 0)
                    {
                        var accountStateRootHash
                            = ModelSerializer.Deserialize<HashDigest<SHA256>>(hash);
                        Trie.Trie accountStateTrie =
                            (Trie.Trie)GetStateRoot(accountStateRootHash);
                        if (!accountStateTrie.IsCommitted)
                        {
                            throw new ArgumentException(
                                $"Failed to find a state root for given " +
                                $"state root hash {accountStateRootHash}.");
                        }

                        foreach (var (key, value) in accountStateTrie)
                        {
                            targetKeyValueStore[key] = StateKeyValueStore[key];
                            count++;
                        }
                    }
                }
            }
        }

        stopwatch.Stop();
        _logger.Debug(
            "Finished copying all states with {Count} key value pairs " +
            "in {ElapsedMilliseconds} ms",
            count,
            stopwatch.ElapsedMilliseconds);
        _logger.Verbose("Finished {MethodName}()", nameof(CopyStates));
    }

    /// <inheritdoc cref="IStateStore.GetStateRoot(HashDigest{SHA256})"/>
    public ITrie GetStateRoot(HashDigest<SHA256> stateRootHash)
        => new Trie.Trie(new HashNode(stateRootHash) { KeyValueStore = StateKeyValueStore });

    /// <inheritdoc cref="System.IDisposable.Dispose()"/>
    public void Dispose()
    {
        if (!_disposed)
        {
            StateKeyValueStore.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
