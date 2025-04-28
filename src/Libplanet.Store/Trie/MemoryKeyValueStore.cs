using System.Collections.Concurrent;
using Bencodex.Types;
using Libplanet.Types.Blocks;

namespace Libplanet.Store.Trie;

public sealed class MemoryKeyValueStore : IKeyValueStore
{
    private readonly ConcurrentDictionary<KeyBytes, byte[]> _dictionary = new(
    [
        // new KeyValuePair<KeyBytes, byte[]>(new KeyBytes(default(BlockHash).Bytes), new Codec().Encode(Null.Value)),
    ]);

    IEnumerable<KeyBytes> IKeyValueStore.Keys => _dictionary.Keys;

    public byte[] this[in KeyBytes keyBytes]
    {
        get => _dictionary[keyBytes];
        set => _dictionary[keyBytes] = value;
    }

    bool IKeyValueStore.Remove(in KeyBytes keyBytes) => _dictionary.TryRemove(keyBytes, out _);

    bool IKeyValueStore.ContainsKey(in KeyBytes keyBytes) => _dictionary.ContainsKey(keyBytes);

    void IDisposable.Dispose()
    {
        // Do nothing.
    }
}
