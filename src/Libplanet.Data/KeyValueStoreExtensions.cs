namespace Libplanet.Data;

public static class KeyValueStoreExtensions
{
    public static void SetMany(
        this IDictionary<string, byte[]> @this, IEnumerable<KeyValuePair<string, byte[]>> values)
    {
        foreach (var (key, value) in values)
        {
            @this[key] = value;
        }
    }

    public static int RemoveMany(this IDictionary<string, byte[]> @this, IEnumerable<string> keys)
        => keys.Count(@this.Remove);
}
