using Libplanet.Store.Trie;

namespace Libplanet.Store;

public interface IKeyValueStore : IDictionary<KeyBytes, byte[]>
{
}
