namespace Libplanet.Store.Trie;

public static class KeyValueStoreExtensions
{
    public static void SetMany(
        this IDictionary<KeyBytes, byte[]> @this, IEnumerable<KeyValuePair<KeyBytes, byte[]>> values)
    {
        foreach (var (key, value) in values)
        {
            @this[key] = value;
        }
    }

    public static int RemoveMany(this IDictionary<KeyBytes, byte[]> @this, IEnumerable<KeyBytes> keys)
        => keys.Count(@this.Remove);
}
