using Libplanet.Store.Trie;

namespace Libplanet.Store;

public static class ITableExtensions
{
    public static void AddRange(
        this ITable @this,
        IEnumerable<KeyValuePair<KeyBytes, byte[]>> keyValuePairs)
    {
        foreach (var kvp in keyValuePairs)
        {
            @this.Add(kvp);
        }
    }

    public static void RemoveRange(this ITable @this, IEnumerable<KeyBytes> keys)
    {
        foreach (var key in keys)
        {
            @this.Remove(key);
        }
    }
}
