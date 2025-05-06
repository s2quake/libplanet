using System.Collections.Concurrent;

namespace Libplanet.Store.Trie;

public sealed class MemoryKeyValueStore : IKeyValueStore
{
    private readonly ConcurrentDictionary<KeyBytes, byte[]> _dictionary = new(
    [
    ]);

    IEnumerable<KeyBytes> IKeyValueStore.Keys => _dictionary.Keys;

    public byte[] this[in KeyBytes keyBytes]
    {
        get => _dictionary[keyBytes];
        set => _dictionary[keyBytes] = value;
    }

    bool IKeyValueStore.Remove(in KeyBytes key) => _dictionary.TryRemove(key, out _);

    bool IKeyValueStore.ContainsKey(in KeyBytes key) => _dictionary.ContainsKey(key);

    void IDisposable.Dispose()
    {
        // Do nothing.
    }
}
