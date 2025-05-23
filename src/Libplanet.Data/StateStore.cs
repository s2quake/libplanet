using System.Security.Cryptography;
using Libplanet.Data.Structures;
using Libplanet.Types;

namespace Libplanet.Data;

public partial class StateStore(ITable table)
{
    private readonly ITable _table = table;

    public StateStore()
        : this(new MemoryDatabase())
    {
    }

    public StateStore(IDatabase database)
        : this(database.GetOrAdd("trie_state_store"))
    {
    }

    public void CopyStates(
        IImmutableSet<HashDigest<SHA256>> stateRootHashes, StateStore targetStateStore)
    {
        var targetKeyValueStore = targetStateStore._table;
        var count = 0L;

        foreach (HashDigest<SHA256> stateRootHash in stateRootHashes)
        {
            var stateTrie = (Trie)GetStateRoot(stateRootHash);
            if (!stateTrie.IsCommitted)
            {
                throw new ArgumentException(
                    $"Failed to find a state root for given state root hash {stateRootHash}.");
            }

            foreach (var (key, value) in stateTrie)
            {
                targetKeyValueStore[key] = _table[key];
                count++;
            }

            // FIXME: Probably not the right place to implement this.
            // It'd be better to have it in Libplanet.Action.State.
            if (stateTrie[string.Empty] is { } metadata)
            {
                foreach (var (path, hash) in stateTrie)
                {
                    // Ignore metadata
                    if (path.Length > 0)
                    {
                        // var accountStateRootHash
                        //     = ModelSerializer.DeserializeFromBytes<HashDigest<SHA256>>(hash);
                        var accountStateRootHash = (HashDigest<SHA256>)hash;
                        Trie accountStateTrie =
                            (Trie)GetStateRoot(accountStateRootHash);
                        if (!accountStateTrie.IsCommitted)
                        {
                            throw new ArgumentException(
                                $"Failed to find a state root for given " +
                                $"state root hash {accountStateRootHash}.");
                        }

                        foreach (var (key, value) in accountStateTrie)
                        {
                            targetKeyValueStore[key] = _table[key];
                            count++;
                        }
                    }
                }
            }
        }
    }

    public ITrie GetStateRoot(HashDigest<SHA256> stateRootHash)
        => stateRootHash == default ? new Trie() : Trie.Create(stateRootHash, _table);
}
