using Libplanet.Types;
using Libplanet.Types.Tests;
using System.Collections;
using Xunit;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests;

public abstract class KeyedIndexTestBase<TKey, TValue>(ITestOutputHelper output)
    where TKey : notnull
    where TValue : IHasKey<TKey>
{
    protected abstract TKey CreateKey(Random random);

    protected abstract TValue CreateValue(Random random);

    protected abstract KeyedIndexBase<TKey, TValue> CreateIndex(bool useCache);

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
        var value = CreateValue(random);
        index.Add(value);

        Assert.Equal(value, index[value.Key]);

        index.ClearCache();
        Assert.Equal(value, index[value.Key]);

        var nonExistentKey = CreateKey(random, item => !Equals(item, value.Key));
        Assert.Throws<KeyNotFoundException>(() => index[nonExistentKey]);
    }

    [Fact]
    public void Set_WithDifferentKey_Throw()
    {
        var random = GetRandom(output);
        var index = CreateIndex(false);
        var value = CreateValue(random);
        var key = CreateKey(random);

        Assert.Throws<ArgumentException>(() => index[key] = value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Remove(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateValues(random, 10);
        foreach (var keyValue in keyValues)
        {
            index.Add(keyValue);
        }

        Assert.True(index.Remove(keyValues[0].Key));
        Assert.Throws<KeyNotFoundException>(() => index[keyValues[0].Key]);

        Assert.True(index.Remove(keyValues[1]));
        Assert.Throws<KeyNotFoundException>(() => index[keyValues[1].Key]);

        var nonExistentKey = CreateKey(random, item => !keyValues.Select(v => v.Key).Contains(item));
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
        var keyValues = CreateValues(random, 10);
        foreach (var keyValue in keyValues)
        {
            index.Add(keyValue);
        }

        var keysToRemove = keyValues.Take(5).Select(v => v.Key).ToArray();
        var valuesToRemove2 = keyValues.Skip(5).ToArray();
        index.RemoveRange(keysToRemove);
        Assert.Equal(5, index.Count);

        foreach (var value in keyValues)
        {
            if (keysToRemove.Contains(value.Key))
            {
                Assert.Throws<KeyNotFoundException>(() => index[value.Key]);
            }
            else
            {
                Assert.Equal(value, index[value.Key]);
            }
        }

        index.RemoveRange(valuesToRemove2);
        Assert.Empty(index);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryAdd(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var value = CreateValue(random);

        Assert.True(index.TryAdd(value));
        Assert.Equal(value, index[value.Key]);

        Assert.False(index.TryAdd(value));
        Assert.Equal(value, index[value.Key]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Add(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var value = CreateValue(random);

        index.Add(value);
        Assert.Equal(value, index[value.Key]);

        Assert.Throws<ArgumentException>(() => index.Add(value));

        var nonExistentKey = CreateKey(random, item => !Equals(item, value.Key));
        Assert.Throws<KeyNotFoundException>(() => index[nonExistentKey]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddRange(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var values = CreateValues(random, 10);

        index.AddRange(values);
        Assert.Equal(10, index.Count);

        foreach (var value in values)
        {
            Assert.Equal(value, index[value.Key]);
        }

        Assert.Throws<ArgumentException>(() => index.AddRange(values));

        var newValue = CreateValue(random, item => !values.Contains(item));
        TValue[] valuesToAdd = [newValue, .. values];

        Assert.Throws<ArgumentException>(() => index.AddRange(valuesToAdd));
        Assert.Equal(11, index.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContainsKey(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var value = CreateValue(random);
        index.Add(value);

        Assert.True(index.ContainsKey(value.Key));

        var nonExistentKey = CreateKey(random, item => !Equals(item, value.Key));
        Assert.False(index.ContainsKey(nonExistentKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryGetValue(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var value = CreateValue(random);
        index.Add(value);

        Assert.True(index.TryGetValue(value.Key, out var actualValue));
        Assert.Equal(value, actualValue);

        var nonExistentKey = CreateKey(random, item => !Equals(item, value.Key));
        Assert.False(index.TryGetValue(nonExistentKey, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Clear(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var values = CreateValues(random, 10);
        foreach (var value in values)
        {
            index.Add(value);
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

        var values = CreateValues(random, 10);
        foreach (var value in values)
        {
            index.Add(value);
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
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var values = CreateValues(random, 5);
        foreach (var value in values)
        {
            index.Add(value);
        }

        var keys = index.Keys;

        // ICollection<TKey>.Count
        Assert.Equal(5, keys.Count);

        // ICollection<TKey>.IsReadOnly
        Assert.True(keys.IsReadOnly);

        // ICollection<TKey>.Contains
        foreach (var value in values)
        {
            Assert.Contains(value.Key, keys);
        }
        var nonExistentKey = CreateKey(random, item => !values.Select(v => v.Key).Contains(item));
        Assert.False(keys.Contains(nonExistentKey));

        // ICollection<TKey>.CopyTo
        var array = new TKey[5];
        keys.CopyTo(array, 0);
        Assert.All(values.Select(v => v.Key), k => Assert.Contains(k, array));
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

        Assert.All(keyList1, k => Assert.Contains(k, keys));

        // IEnumerable.GetEnumerator
        var keyList2 = new List<TKey>();
        var enumerator2 = ((IEnumerable)keys).GetEnumerator();
        while (enumerator2.MoveNext())
        {
            keyList2.Add((TKey)enumerator2.Current);
        }

        Assert.All(keyList2, k => Assert.Contains(k, keys));

        // ICollection<TKey>.Add, Clear, Remove는 NotSupportedException
        Assert.Throws<NotSupportedException>(() => keys.Add(nonExistentKey));
        Assert.Throws<NotSupportedException>(() => keys.Clear());
        Assert.Throws<NotSupportedException>(() => keys.Remove(values[0].Key));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Values(bool useCache)
    {
        // Values
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateValues(random, 5);
        foreach (var keyValue in keyValues)
        {
            index[keyValue.Key] = keyValue;
        }

        var values = index.Values;

        // ICollection<TValue>.Count
        Assert.Equal(5, values.Count);

        // ICollection<TValue>.IsReadOnly
        Assert.True(values.IsReadOnly);

        // ICollection<TValue>.CopyTo
        var array = new TValue[5];
        values.CopyTo(array, 0);
        Assert.All(keyValues, v => Assert.Contains(v, array));
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

        Assert.All(valueList1, v => Assert.Contains(v, keyValues));

        // IEnumerable.GetEnumerator
        var valueList2 = new List<TValue>();
        var enumerator2 = ((IEnumerable)values).GetEnumerator();
        while (enumerator2.MoveNext())
        {
            valueList2.Add((TValue)enumerator2.Current);
        }

        Assert.All(valueList2, v => Assert.Contains(v, keyValues));

        // ICollection<TValue>.Add, Clear, Remove, Contains는 NotSupportedException
        var nonExistentValue = CreateValue(random, item => !keyValues.Contains(item));
        Assert.Throws<NotSupportedException>(() => values.Add(nonExistentValue));
        Assert.Throws<NotSupportedException>(() => values.Clear());
        Assert.Throws<NotSupportedException>(() => values.Remove(keyValues[0]));
        Assert.Throws<NotSupportedException>(() => values.Contains(keyValues[0]));
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
        var keyValues = CreateValues(random, 5);
        foreach (var keyValue in keyValues)
        {
            index[keyValue.Key] = keyValue;
        }

        var keys = ((IReadOnlyDictionary<TKey, TValue>)index).Keys;
        foreach (var keyValue in keyValues)
        {
            Assert.Contains(keyValue.Key, keys);
        }

        var nonExistentKey = CreateKey(random, item => !keyValues.Select(kv => kv.Key).Contains(item));
        Assert.DoesNotContain(nonExistentKey, keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IReadOnlyDictionary_Values(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateValues(random, 5);
        foreach (var keyValue in keyValues)
        {
            index[keyValue.Key] = keyValue;
        }

        var values = ((IReadOnlyDictionary<TKey, TValue>)index).Values;
        foreach (var keyValue in keyValues)
        {
            Assert.Contains(keyValue, values);
        }

        var nonExistentValue = CreateValue(random, item => !keyValues.Contains(item));
        Assert.DoesNotContain(nonExistentValue, values);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ICollection_Add(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValue = CreateValue(random);

        var collection = (ICollection<KeyValuePair<TKey, TValue>>)index;
        collection.Add(new KeyValuePair<TKey, TValue>(keyValue.Key, keyValue));
        Assert.Equal(keyValue, index[keyValue.Key]);

        Assert.Throws<ArgumentException>(() => collection.Add(new KeyValuePair<TKey, TValue>(keyValue.Key, keyValue)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ICollection_Contains(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValue = CreateValue(random);
        index[keyValue.Key] = keyValue;

        var collection = (ICollection<KeyValuePair<TKey, TValue>>)index;
        Assert.True(collection.Contains(new KeyValuePair<TKey, TValue>(keyValue.Key, keyValue)));

        var nonExistentKey = CreateKey(random, item => !Equals(item, keyValue.Key));
        Assert.False(collection.Contains(new KeyValuePair<TKey, TValue>(nonExistentKey, keyValue)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ICollection_CopyTo(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateValues(random, 5);
        foreach (var keyValue in keyValues)
        {
            index[keyValue.Key] = keyValue;
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
        var keyValue = CreateValue(random);
        index[keyValue.Key] = keyValue;

        var collection = (ICollection<KeyValuePair<TKey, TValue>>)index;
        Assert.True(collection.Remove(new KeyValuePair<TKey, TValue>(keyValue.Key, keyValue)));
        Assert.Throws<KeyNotFoundException>(() => index[keyValue.Key]);

        var nonExistentKey = CreateKey(random, item => !Equals(item, keyValue.Key));
        Assert.False(collection.Remove(new KeyValuePair<TKey, TValue>(nonExistentKey, keyValue)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IEnumerable_Generic_GetEnumerator(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateValues(random, 5);
        foreach (var keyValue in keyValues)
        {
            index[keyValue.Key] = keyValue;
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

        foreach (var keyValue in keyValues)
        {
            Assert.Contains(keyValue.Key, keyList);
            Assert.Contains(keyValue, valueList);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IEnumerable_GetEnumerator(bool useCache)
    {
        var random = GetRandom(output);
        var index = CreateIndex(useCache);
        var keyValues = CreateValues(random, 5);
        foreach (var keyValue in keyValues)
        {
            index[keyValue.Key] = keyValue;
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

        foreach (var keyValue in keyValues)
        {
            Assert.Contains(keyValue.Key, keyList);
            Assert.Contains(keyValue, valueList);
        }
    }

    protected static Random GetRandom(ITestOutputHelper output)
    {
        var seed = RandomUtility.Int32();
        output.WriteLine($"Random seed: {seed}");
        return new Random(seed);
    }
}
