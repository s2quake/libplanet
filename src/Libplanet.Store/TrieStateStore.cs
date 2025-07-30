using System.Diagnostics;
using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Store.Trie;
using Libplanet.Types;
using Serilog;

namespace Libplanet.Store;

public partial class TrieStateStore(IDictionary<KeyBytes, byte[]> keyValueStore)
{
    private readonly ILogger _logger = Log.ForContext<TrieStateStore>();
    private readonly HashNodeCache _cache = new();
    private bool _isDisposed = false;

    public TrieStateStore()
        : this(new MemoryKeyValueStore())
    {
    }

    public IDictionary<KeyBytes, byte[]> StateKeyValueStore => keyValueStore;

    public void CopyStates(
        IImmutableSet<HashDigest<SHA256>> stateRootHashes, TrieStateStore targetStateStore)
    {
        IDictionary<KeyBytes, byte[]> targetKeyValueStore = targetStateStore.StateKeyValueStore;
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

    public ITrie GetStateRoot(HashDigest<SHA256> stateRootHash)
        => stateRootHash == default ? new Trie.Trie() : Trie.Trie.Create(stateRootHash, StateKeyValueStore);

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // StateKeyValueStore.Dispose();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
