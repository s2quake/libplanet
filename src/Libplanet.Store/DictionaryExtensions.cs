using Libplanet.Types;

namespace Libplanet.Store;

public static class DictionaryExtensions
{
    public static void Add<TKey, TValue>(this IDictionary<TKey, TValue> @this, TValue value)
        where TKey : notnull
        where TValue : IHasKey<TKey>
        => @this.Add(value.Key, value);

    public static void Remove<TKey, TValue>(this IDictionary<TKey, TValue> @this, TValue value)
        where TKey : notnull
        where TValue : IHasKey<TKey>
        => @this.Remove(value.Key);
}
