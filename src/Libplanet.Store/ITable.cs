using Libplanet.Store.Trie;

namespace Libplanet.Store;

public interface ITable : IDictionary<KeyBytes, byte[]>
{
}
