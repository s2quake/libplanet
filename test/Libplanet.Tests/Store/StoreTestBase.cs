using Libplanet.Data;
using Libplanet.Types;

namespace Libplanet.Tests.Store;

public abstract class StoreTestBase<TKey, TValue>(StoreBase<TKey, TValue> store)
    where TKey : notnull
    where TValue : notnull
{
    protected abstract KeyValuePair<TKey, TValue> CreateItem();

    [Fact]
    public void Add()
    {
        var item = CreateItem();
        store.Add(item.Key, item.Value);
        Assert.True(store.ContainsKey(item.Key));
    }

    [Fact]
    public void AddWithIHasKey()
    {
        var item = CreateItem();

        if (item.Value is IHasKey<TKey>)
        {
            store.GetType().GetMethod("Add")!
                .MakeGenericMethod(typeof(IHasKey<TKey>))
                .Invoke(store, [item.Value]);
            Assert.True(store.ContainsKey(item.Key));
        }
    }
}
