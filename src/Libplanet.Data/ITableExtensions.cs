namespace Libplanet.Data;

public static class ITableExtensions
{
    public static void AddRange(
        this ITable @this,
        IEnumerable<KeyValuePair<string, byte[]>> keyValuePairs)
    {
        foreach (var kvp in keyValuePairs)
        {
            @this.Add(kvp);
        }
    }

    public static void RemoveRange(this ITable @this, IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            @this.Remove(key);
        }
    }
}
