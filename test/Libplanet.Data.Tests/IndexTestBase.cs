using System.Collections;
using System.Collections.Concurrent;
using Libplanet.Types.Tests;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public abstract class IndexTestBase<TKey, TValue, TIndex, TDatabase>(ITestOutputHelper output) : IDisposable
    where TKey : notnull
    where TValue : notnull
    where TIndex : IndexBase<TKey, TValue>
    where TDatabase : IDatabase
{
    private readonly ConcurrentBag<TDatabase> _databases = [];
    private bool _disposedValue;

    protected abstract TKey CreateKey(Random random);

    protected abstract TValue CreateValue(Random random);

    protected abstract TIndex CreateIndex(TDatabase database, bool useCache);

    protected abstract TDatabase CreateDatabase(string name);

    protected abstract void DeleteDatabase(TDatabase database);

    protected TIndex CreateIndex(string name, bool useCache)
    {
        var database = CreateDatabase(name);
        var index = CreateIndex(database, useCache);
        _databases.Add(database);
        return index;
    }

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

    protected KeyValuePair<TKey, TValue> CreateKeyValue(Random random)
        => new(CreateKeys(random, 1)[0], CreateValues(random, 1)[0]);

    protected KeyValuePair<TKey, TValue>[] CreateKeyValues(Random random, int length)
    {
        var keys = CreateKeys(random, length);
        var values = CreateValues(random, length);
        return [.. keys.Zip(values, (k, v) => new KeyValuePair<TKey, TValue>(k, v))];
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
        var random = GetRandom();
        var index = CreateIndex(nameof(Get), useCache);
        var (key, value) = CreateKeyValue(random);
        index[key] = value;

        Assert.Equal(value, index[key]);

        index.ClearCache();
        Assert.Equal(value, index[key]);

        var nonExistentKey = CreateKey(random, item => !Equals(item, key));
        Assert.Throws<KeyNotFoundException>(() => index[nonExistentKey]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Remove(bool useCache)
    {
        var random = GetRandom();
        var index = CreateIndex(nameof(Remove), useCache);
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
        var random = GetRandom();
        var index = CreateIndex(nameof(RemoveRange), useCache);
        var keyValues = CreateKeyValues(random, 12);
        index.UpsertRange(keyValues);

        var keysToRemove = keyValues.Take(5).Select(kv => kv.Key).ToArray();
        Assert.Equal(5, index.RemoveRange(keysToRemove));
        Assert.Equal(7, index.Count);

        foreach (var (key, _) in keyValues)
        {
            if (keysToRemove.Contains(key))
            {
                Assert.Throws<KeyNotFoundException>(() => index[key]);
            }
            else
            {
                Assert.Equal(index[key], keyValues.First(kv => kv.Key.Equals(key)).Value);
            }
        }

        Assert.Equal(7, index.RemoveRange(keyValues.Select(kv => kv.Key)));
        Assert.Empty(index);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryAdd(bool useCache)
    {
        var random = GetRandom();
        var index = CreateIndex(nameof(TryAdd), useCache);
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
        var random = GetRandom();
        var index = CreateIndex(nameof(Add), useCache);
        var (key, value) = CreateKeyValue(random);

        index.Add(key, value);
        Assert.Equal(value, index[key]);

        Assert.Throws<ArgumentException>(() => index.Add(key, value));

        var nonExistentKey = CreateKey(random, item => !Equals(item, key));
        Assert.Throws<KeyNotFoundException>(() => index[nonExistentKey]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddRange(bool useCache)
    {
        var random = GetRandom();
        var index = CreateIndex(nameof(AddRange), useCache);
        var keyValues = CreateKeyValues(random, 10);

        var keyValuesToAdd1 = keyValues.Take(5).ToArray();
        index.AddRange(keyValuesToAdd1);
        Assert.Equal(5, index.Count);

        foreach (var (key, value) in keyValuesToAdd1)
        {
            Assert.Equal(value, index[key]);
        }

        Assert.Throws<ArgumentException>(() => index.AddRange(keyValues));
        var keyValuesToAdd2 = keyValues.Skip(5).ToArray();
        index.AddRange(keyValuesToAdd2);
        Assert.Equal(10, index.Count);

        foreach (var (key, value) in keyValuesToAdd2)
        {
            Assert.Equal(value, index[key]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpsertRange(bool useCache)
    {
        var random = GetRandom();
        var index = CreateIndex(nameof(UpsertRange), useCache);
        var keyValues = CreateKeyValues(random, 10);

        // Upsert existing keys
        var existingKeyValues = keyValues.Take(5).ToArray();
        index.UpsertRange(existingKeyValues);
        Assert.Equal(5, index.Count);

        foreach (var (key, value) in existingKeyValues)
        {
            Assert.Equal(value, index[key]);
        }

        // Upsert new keys
        var newKeyValues = keyValues.Skip(5).ToArray();
        index.UpsertRange(newKeyValues);
        Assert.Equal(10, index.Count);

        foreach (var (key, value) in newKeyValues)
        {
            Assert.Equal(value, index[key]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContainsKey(bool useCache)
    {
        var random = GetRandom();
        var index = CreateIndex(nameof(ContainsKey), useCache);
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
        var random = GetRandom();
        var index = CreateIndex(nameof(TryGetValue), useCache);
        var (key, value) = CreateKeyValue(random);
        index[key] = value;

        Assert.True(index.TryGetValue(key, out var actualValue1));
        Assert.Equal(value, actualValue1);
        index.ClearCache();
        Assert.True(index.TryGetValue(key, out var actualValue2));
        Assert.Equal(value, actualValue2);

        var nonExistentKey = CreateKey(random, item => !Equals(item, key));
        Assert.False(index.TryGetValue(nonExistentKey, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Clear(bool useCache)
    {
        var random = GetRandom();
        var index = CreateIndex(nameof(Clear), useCache);
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
        var random = GetRandom();
        var index = CreateIndex(nameof(Count), useCache);
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
        var random = GetRandom();
        var index = CreateIndex(nameof(Keys), useCache);
        var keyValues = CreateKeyValues(random, 5);
        foreach (var (key, value) in keyValues)
        {
            index[key] = value;
        }

        var keys = index.Keys;

        // ICollection<TKey>.Count
        Assert.Equal(5, keys.Count);

        // ICollection<TKey>.IsReadOnly
        Assert.True(keys.IsReadOnly);

        // ICollection<TKey>.Contains
        foreach (var (key, _) in keyValues)
        {
            Assert.Contains(key, keys);
        }
        var nonExistentKey = CreateKey(random, item => !keyValues.Select(kv => kv.Key).Contains(item));
        Assert.False(keys.Contains(nonExistentKey));

        // ICollection<TKey>.CopyTo
        var array = new TKey[5];
        keys.CopyTo(array, 0);
        Assert.All(keyValues.Select(kv => kv.Key), k => Assert.Contains(k, array));
        Assert.Throws<ArgumentOutOfRangeException>(() => keys.CopyTo(array, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => keys.CopyTo(array, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => keys.CopyTo(new TKey[1], 0));

        // ICollection<TKey>.GetEnumerator
        var keyList1 = new List<TKey>();
        var enumerator1 = keys.GetEnumerator();
        while (enumerator1.MoveNext())
        {
            keyList1.Add(enumerator1.Current);
        }

        Assert.All(keyValues.Select(kv => kv.Key), k => Assert.Contains(k, keyList1));

        // IEnumerable.GetEnumerator
        var keyList2 = new List<TKey>();
        var enumerator2 = ((IEnumerable)keys).GetEnumerator();
        while (enumerator2.MoveNext())
        {
            keyList2.Add((TKey)enumerator2.Current);
        }

        Assert.All(keyValues.Select(kv => kv.Key), k => Assert.Contains(k, keyList2));

        // ICollection<TKey>.Add, Clear, Remove는 NotSupportedException
        Assert.Throws<NotSupportedException>(() => keys.Add(nonExistentKey));
        Assert.Throws<NotSupportedException>(() => keys.Clear());
        Assert.Throws<NotSupportedException>(() => keys.Remove(keyValues[0].Key));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Values(bool useCache)
    {
        // Values
        var random = GetRandom();
        var index = CreateIndex(nameof(Values), useCache);
        var keyValues = CreateKeyValues(random, 5);
        foreach (var (key, value) in keyValues)
        {
            index[key] = value;
        }

        var values = index.Values;

        // ICollection<TValue>.Count
        Assert.Equal(5, values.Count);

        // ICollection<TValue>.IsReadOnly
        Assert.True(values.IsReadOnly);

        // ICollection<TValue>.CopyTo
        var array = new TValue[5];
        values.CopyTo(array, 0);
        Assert.All(keyValues.Select(kv => kv.Value), v => Assert.Contains(v, array));
        Assert.Throws<ArgumentOutOfRangeException>(() => values.CopyTo(array, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => values.CopyTo(array, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => values.CopyTo(new TValue[1], 0));

        // ICollection<TValue>.GetEnumerator
        var valueList1 = new List<TValue>();
        var enumerator1 = values.GetEnumerator();
        while (enumerator1.MoveNext())
        {
            valueList1.Add(enumerator1.Current);
        }

        Assert.All(keyValues.Select(kv => kv.Value), v => Assert.Contains(v, valueList1));

        // IEnumerable.GetEnumerator
        var valueList2 = new List<TValue>();
        var enumerator2 = ((IEnumerable)values).GetEnumerator();
        while (enumerator2.MoveNext())
        {
            valueList2.Add((TValue)enumerator2.Current);
        }

        Assert.All(keyValues.Select(kv => kv.Value), v => Assert.Contains(v, valueList2));

        // ICollection<TValue>.Add, Clear, Remove, Contains는 NotSupportedException
        var nonExistentValue = CreateValue(random, item => !keyValues.Select(kv => kv.Value).Contains(item));
        Assert.Throws<NotSupportedException>(() => values.Add(nonExistentValue));
        Assert.Throws<NotSupportedException>(() => values.Clear());
        Assert.Throws<NotSupportedException>(() => values.Remove(keyValues[0].Value));
        Assert.Throws<NotSupportedException>(() => values.Contains(keyValues[0].Value));
    }

    [Fact]
    public void IsReadOnly()
    {
        var index = CreateIndex(nameof(IsReadOnly), false);
        Assert.False(index.IsReadOnly);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IReadOnlyDictionary_Keys(bool useCache)
    {
        var random = GetRandom();
        var index = CreateIndex(nameof(IReadOnlyDictionary_Keys), useCache);
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

        var nonExistentKey = CreateKey(random, item => !keyValues.Select(kv => kv.Key).Contains(item));
        Assert.DoesNotContain(nonExistentKey, keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IReadOnlyDictionary_Values(bool useCache)
    {
        var random = GetRandom();
        var index = CreateIndex(nameof(IReadOnlyDictionary_Values), useCache);
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

        var nonExistentValue = CreateValue(random, item => !keyValues.Select(kv => kv.Value).Contains(item));
        Assert.DoesNotContain(nonExistentValue, values);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ICollection_Add(bool useCache)
    {
        var random = GetRandom();
        var index = CreateIndex(nameof(ICollection_Add), useCache);
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
        var random = GetRandom();
        var index = CreateIndex(nameof(ICollection_Contains), useCache);
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
        var random = GetRandom();
        var index = CreateIndex(nameof(ICollection_CopyTo), useCache);
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
        var random = GetRandom();
        var index = CreateIndex(nameof(ICollection_Remove), useCache);
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
        var random = GetRandom();
        var index = CreateIndex(nameof(IEnumerable_Generic_GetEnumerator), useCache);
        var keyValues = CreateKeyValues(random, 5);
        foreach (var (key, value) in keyValues)
        {
            index[key] = value;
        }

        var enumerator = ((IEnumerable<KeyValuePair<TKey, TValue>>)index).GetEnumerator();
        var keyList = new List<TKey>();
        var valueList = new List<TValue>();
        while (enumerator.MoveNext())
        {
            var (key, value) = enumerator.Current;
            keyList.Add(key);
            valueList.Add(value);
        }

        foreach (var (key, value) in keyValues)
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
        var random = GetRandom();
        var index = CreateIndex(nameof(IEnumerable_GetEnumerator), useCache);
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

    protected Random GetRandom()
    {
        var seed = RandomUtility.Int32();
        output.WriteLine($"Random seed: {seed}");
        return new Random(seed);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                foreach (var database in _databases)
                {
                    DeleteDatabase(database);
                }

                _databases.Clear();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
