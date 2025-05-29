using Libplanet.Types;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public abstract class KeyedIndexTestBase<TKey, TValue>(ITestOutputHelper output)
    where TKey : notnull
    where TValue : IHasKey<TKey>
{
    protected abstract TKey CreateKey(Random random);

    protected abstract TValue CreateValue(Random random);

    protected abstract KeyedIndexBase<TKey, TValue> CreateIndex();

    protected TKey[] CreateKeys(Random random, int length)
    {
        var keyList = new List<TKey>(length);
        for (int i = 0; i < length; i++)
        {
            var key = CreateKey(random, item => !keyList.Contains(item));
            keyList.Add(key);
        }

        return [.. keyList];
    }

    protected TValue[] CreateValues(Random random, int length)
    {
        var valueList = new List<TValue>(length);
        for (int i = 0; i < length; i++)
        {
            var value = CreateValue(random, item => !valueList.Contains(item));
            valueList.Add(value);
        }

        return [.. valueList];
    }

    protected (TKey, TValue) CreateKeyValue(Random random)
        => (CreateKeys(random, 1)[0], CreateValues(random, 1)[0]);

    protected (TKey, TValue)[] CreateKeyValues(Random random, int length)
    {
        var keys = CreateKeys(random, length);
        var values = CreateValues(random, length);
        return [.. keys.Zip(values, (k, v) => (k, v))];
    }

    protected TKey CreateKey(Random random, Func<TKey, bool> predicate)
    {
        TKey key;
        do
        {
            key = RandomUtility.Try(random, item => CreateKey(random), predicate);
        } while (!predicate(key));
        return key;
    }

    protected TValue CreateValue(Random random, Func<TValue, bool> predicate)
    {
        TValue value;
        do
        {
            value = RandomUtility.Try(random, item => CreateValue(random), predicate);
        } while (!predicate(value));
        return value;
    }

    [Fact]
    public void Get()
    {
        var random = GetRandom(output);
        var index = CreateIndex();
        var (key, value) = CreateKeyValue(random);
        index[key] = value;
        Assert.Equal(value, index[key]);

        var nonExistentKey = CreateKey(random, item => !Equals(item, key));
        Assert.Throws<KeyNotFoundException>(() => index[nonExistentKey]);
    }

    [Fact]
    public void Remove()
    {
        var random = GetRandom(output);
        var index = CreateIndex();
        var (key, value) = CreateKeyValue(random);
        index[key] = value;
        Assert.Equal(value, index[key]);

        Assert.True(index.Remove(key));
        Assert.Throws<KeyNotFoundException>(() => index[key]);

        var nonExistentKey = CreateKey(random, item => !Equals(item, key));
        Assert.False(index.Remove(nonExistentKey));
        Assert.Throws<KeyNotFoundException>(() => index[nonExistentKey]);
    }

    protected static Random GetRandom(ITestOutputHelper output)
    {
        var seed = RandomUtility.Int32();
        output.WriteLine($"Random seed: {seed}");
        return new Random(seed);
    }
}
