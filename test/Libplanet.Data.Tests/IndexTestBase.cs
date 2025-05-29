using System.Collections;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Count(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        Assert.Empty(index);

        var keyValues = CreateKeyValues(random, 10);
        foreach (var (key, value) in keyValues)
        {
            index[key] = value;
        }

        Assert.Equal(10, index.Count);

        index.Clear();
        Assert.Empty(index);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Keys(bool useCache)
    {
        // Keys
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateKeyValues(random, 5);
        foreach (var (key, value) in keyValues)
        {
            index[key] = value;
        }

        // ICollection<TKey>.Count
        Assert.Equal(5, index.Keys.Count);

        // ICollection<TKey>.IsReadOnly
        Assert.True(index.Keys.IsReadOnly);

        // ICollection<TKey>.Contains
        foreach (var (key, _) in keyValues)
        {
            Assert.True(index.Keys.Contains(key));
        }
        var nonExistentKey = CreateKey(random, item => !keyValues.Select(kv => kv.Item1).Contains(item));
        Assert.False(index.Keys.Contains(nonExistentKey));

        // ICollection<TKey>.CopyTo
        var array = new TKey[5];
        index.Keys.CopyTo(array, 0);
        Assert.All(keyValues.Select(kv => kv.Item1), k => Assert.Contains(k, array));

        // ICollection<TKey>.GetEnumerator
        var keys = index.Keys.ToList();
        Assert.All(keyValues.Select(kv => kv.Item1), k => Assert.Contains(k, keys));

        // ICollection<TKey>.Add, Clear, Remove는 NotSupportedException
        Assert.Throws<NotSupportedException>(() => index.Keys.Add(nonExistentKey));
        Assert.Throws<NotSupportedException>(() => index.Keys.Clear());
        Assert.Throws<NotSupportedException>(() => index.Keys.Remove(keyValues[0].Item1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Values(bool useCache)
    {
        // Values
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateKeyValues(random, 5);
        foreach (var (key, value) in keyValues)
        {
            index[key] = value;
        }

        // ICollection<TValue>.Count
        Assert.Equal(5, index.Values.Count);

        // ICollection<TValue>.IsReadOnly
        Assert.True(index.Values.IsReadOnly);

        // ICollection<TValue>.CopyTo
        var array = new TValue[5];
        index.Values.CopyTo(array, 0);
        Assert.All(keyValues.Select(kv => kv.Item2), v => Assert.Contains(v, array));

        // ICollection<TValue>.GetEnumerator
        var values = index.Values.ToList();
        Assert.All(keyValues.Select(kv => kv.Item2), v => Assert.Contains(v, values));

        // ICollection<TValue>.Add, Clear, Remove, Contains는 NotSupportedException
        var nonExistentValue = CreateValue(random, item => !keyValues.Select(kv => kv.Item2).Contains(item));
        Assert.Throws<NotSupportedException>(() => index.Values.Add(nonExistentValue));
        Assert.Throws<NotSupportedException>(() => index.Values.Clear());
        Assert.Throws<NotSupportedException>(() => index.Values.Remove(keyValues[0].Item2));
        Assert.Throws<NotSupportedException>(() => index.Values.Contains(keyValues[0].Item2));
    }

    [Fact]
    public void IsReadOnly()
    {
        var index = CreateIndex(false);
        Assert.False(index.IsReadOnly);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IReadOnlyDictionary_Keys(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateKeyValues(random, 5);
        foreach (var (key, value) in keyValues)
        {
            index[key] = value;
        }

        var keys = ((IReadOnlyDictionary<TKey, TValue>)index).Keys;
        foreach (var (key, _) in keyValues)
        {
            Assert.Contains(key, keys);
        }

        var nonExistentKey = CreateKey(random, item => !keyValues.Select(kv => kv.Item1).Contains(item));
        Assert.DoesNotContain(nonExistentKey, keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IReadOnlyDictionary_Values(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateKeyValues(random, 5);
        foreach (var (key, value) in keyValues)
        {
            index[key] = value;
        }

        var values = ((IReadOnlyDictionary<TKey, TValue>)index).Values;
        foreach (var (_, value) in keyValues)
        {
            Assert.Contains(value, values);
        }

        var nonExistentValue = CreateValue(random, item => !keyValues.Select(kv => kv.Item2).Contains(item));
        Assert.DoesNotContain(nonExistentValue, values);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ICollection_Add(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var (key, value) = CreateKeyValue(random);

        var collection = (ICollection<KeyValuePair<TKey, TValue>>)index;
        collection.Add(new KeyValuePair<TKey, TValue>(key, value));
        Assert.Equal(value, index[key]);

        Assert.Throws<ArgumentException>(() => collection.Add(new KeyValuePair<TKey, TValue>(key, value)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ICollection_Contains(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var (key, value) = CreateKeyValue(random);
        index[key] = value;

        var collection = (ICollection<KeyValuePair<TKey, TValue>>)index;
        Assert.True(collection.Contains(new KeyValuePair<TKey, TValue>(key, value)));

        var nonExistentKey = CreateKey(random, item => !Equals(item, key));
        Assert.False(collection.Contains(new KeyValuePair<TKey, TValue>(nonExistentKey, value)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ICollection_CopyTo(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateKeyValues(random, 5);
        foreach (var (key, value) in keyValues)
        {
            index[key] = value;
        }

        var array = new KeyValuePair<TKey, TValue>[5];
        var collection = (ICollection<KeyValuePair<TKey, TValue>>)index;
        collection.CopyTo(array, 0);

        foreach (var item in array)
        {
            Assert.Equal(item.Value, index[item.Key]);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => collection.CopyTo(array, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => collection.CopyTo(array, 5));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => collection.CopyTo(new KeyValuePair<TKey, TValue>[1], 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ICollection_Remove(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var (key, value) = CreateKeyValue(random);
        index[key] = value;

        var collection = (ICollection<KeyValuePair<TKey, TValue>>)index;
        Assert.True(collection.Remove(new KeyValuePair<TKey, TValue>(key, value)));
        Assert.Throws<KeyNotFoundException>(() => index[key]);

        var nonExistentKey = CreateKey(random, item => !Equals(item, key));
        Assert.False(collection.Remove(new KeyValuePair<TKey, TValue>(nonExistentKey, value)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IEnumerable_Generic_GetEnumerator(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateKeyValues(random, 5);
        foreach (var (key, value) in keyValues)
        {
            index[key] = value;
        }

        var enumerator = ((IEnumerable<KeyValuePair<TKey, TValue>>)index).GetEnumerator();
        var keyList = new List<TKey>();
        var valueList = new List<TValue>();
        while( enumerator.MoveNext())
        {
            var (key, value) = enumerator.Current;
            keyList.Add(key);
            valueList.Add(value);
        }

        foreach(var (key, value) in keyValues)
        {
            Assert.Contains(key, keyList);
            Assert.Contains(value, valueList);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IEnumerable_GetEnumerator(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateKeyValues(random, 5);
        foreach (var (key, value) in keyValues)
        {
            index[key] = value;
        }

        var enumerator = ((IEnumerable)index).GetEnumerator();
        var keyList = new List<TKey>();
        var valueList = new List<TValue>();
        while (enumerator.MoveNext())
        {
            var kvp = (KeyValuePair<TKey, TValue>)enumerator.Current;
            keyList.Add(kvp.Key);
            valueList.Add(kvp.Value);
        }

        foreach (var (key, value) in keyValues)
        {
            Assert.Contains(key, keyList);
            Assert.Contains(value, valueList);
        }
    }


    protected static Random GetRandom(ITestOutputHelper output)
    {
        var seed = RandomUtility.Int32();
        output.WriteLine($"Random seed: {seed}");
        return new Random(seed);
    }
}
