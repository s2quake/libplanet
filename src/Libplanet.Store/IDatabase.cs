using Libplanet.Store.Trie;

namespace Libplanet.Store;

public interface IDatabase : IReadOnlyDictionary<string, IDictionary<KeyBytes, byte[]>>
{
    IDictionary<KeyBytes, byte[]> GetOrAdd(string key);

    public bool Remove(string key);
}
