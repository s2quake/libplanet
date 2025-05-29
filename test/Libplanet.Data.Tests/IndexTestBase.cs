using System.ComponentModel;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public abstract class IndexTestBase<TKey, TValue>(ITestOutputHelper output)
    where TKey : notnull
    where TValue : notnull
{
    protected abstract TKey CreateKey(Random random);

    protected abstract TValue CreateValue(Random random);

    protected abstract IndexBase<TKey, TValue> CreateIndex(bool useCache);

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Get(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var (key, value) = CreateKeyValue(random);
        index[key] = value;

        Assert.Equal(value, index[key]);

        var nonExistentKey = CreateKey(random, item => !Equals(item, key));
        Assert.Throws<KeyNotFoundException>(() => index[nonExistentKey]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Remove(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var (key, value) = CreateKeyValue(random);
        index[key] = value;
        Assert.Equal(value, index[key]);

        Assert.True(index.Remove(key));
        Assert.Throws<KeyNotFoundException>(() => index[key]);

        var nonExistentKey = CreateKey(random, item => !Equals(item, key));
        Assert.False(index.Remove(nonExistentKey));
        Assert.Throws<KeyNotFoundException>(() => index[nonExistentKey]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemoveRange(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateKeyValues(random, 10);
        foreach (var (key, value) in keyValues)
        {
            index[key] = value;
        }

        var keysToRemove = keyValues.Take(5).Select(kv => kv.Item1).ToArray();
        index.RemoveRange(keysToRemove);
        Assert.Equal(5, index.Count);

        foreach (var (key, _) in keyValues)
        {
            if (keysToRemove.Contains(key))
            {
                Assert.Throws<KeyNotFoundException>(() => index[key]);
            }
            else
            {
                Assert.Equal(index[key], keyValues.First(kv => kv.Item1.Equals(key)).Item2);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryAdd(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var (key, value) = CreateKeyValue(random);

        Assert.True(index.TryAdd(key, value));
        Assert.Equal(value, index[key]);

        // Try adding the same key again should return false
        Assert.False(index.TryAdd(key, value));
        Assert.Equal(value, index[key]);

        Assert.False(index.TryAdd(key, value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Add(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var (key, value) = CreateKeyValue(random);

        index.Add(key, value);
        Assert.Equal(value, index[key]);

        // Adding the same key again should throw an exception
        Assert.Throws<ArgumentException>(() => index.Add(key, value));

        var nonExistentKey = CreateKey(random, item => !Equals(item, key));
        Assert.Throws<KeyNotFoundException>(() => index[nonExistentKey]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContainsKey(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var (key, value) = CreateKeyValue(random);
        index[key] = value;

        Assert.True(index.ContainsKey(key));

        var nonExistentKey = CreateKey(random, item => !Equals(item, key));
        Assert.False(index.ContainsKey(nonExistentKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryGetValue(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var (key, value) = CreateKeyValue(random);
        index[key] = value;

        Assert.True(index.TryGetValue(key, out var actualValue));
        Assert.Equal(value, actualValue);

        var nonExistentKey = CreateKey(random, item => !Equals(item, key));
        Assert.False(index.TryGetValue(nonExistentKey, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Clear(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateKeyValues(random, 10);
        foreach (var (key, value) in keyValues)
        {
            index[key] = value;
        }

        Assert.Equal(10, index.Count);
        index.Clear();
        Assert.Empty(index);
    }

    protected static Random GetRandom(ITestOutputHelper output)
    {
        var seed = RandomUtility.Int32();
        output.WriteLine($"Random seed: {seed}");
        return new Random(seed);
    }
}
