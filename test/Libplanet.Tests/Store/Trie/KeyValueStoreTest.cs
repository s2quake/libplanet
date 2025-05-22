using Libplanet.Store;

namespace Libplanet.Tests.Store.Trie;

public abstract class KeyValueStoreTest
{
    private const int PreStoredDataCount = 10;

    private const int PreStoredDataKeySize = 16;

    private const int PreStoredDataValueSize = 32;

    protected IDictionary<string, byte[]> KeyValueStore { get; set; }

    protected Random Random { get; } = new Random();

    private string[] PreStoredDataKeys { get; set; }

    private byte[][] PreStoredDataValues { get; set; }

    [Fact]
    public void Get()
    {
        foreach (var (key, expectedValue) in PreStoredDataKeys.Zip(
            PreStoredDataValues, ValueTuple.Create))
        {
            var actual = KeyValueStore[key];
            Assert.Equal(expectedValue, actual);
        }

        var randomKey = NewRandomKey();
        Assert.Throws<KeyNotFoundException>(() => KeyValueStore[randomKey]);
    }

    [Fact]
    public void Set()
    {
        var key = RandomUtility.Word();
        byte[] value = Random.NextBytes(PreStoredDataValueSize);
        KeyValueStore[key] = value;

        Assert.Equal(value, KeyValueStore[key]);
    }

    [Fact]
    public void SetMany()
    {
        var values = new Dictionary<string, byte[]>();
        foreach (int i in Enumerable.Range(0, 10))
        {
            values[RandomUtility.Word()] =
                Random.NextBytes(PreStoredDataValueSize);
        }

        KeyValueStore.SetMany(values);

        foreach (KeyValuePair<string, byte[]> kv in values)
        {
            Assert.Equal(kv.Value, KeyValueStore[kv.Key]);
        }
    }

    // This test will cover DefaultKeyValueStore.Set
    [Fact]
    public void Overwrite()
    {
        foreach (var (key, expectedValue) in PreStoredDataKeys.Zip(
            PreStoredDataValues, ValueTuple.Create))
        {
            var randomValue = Random.NextBytes(PreStoredDataValueSize);
            var actual = KeyValueStore[key];
            Assert.Equal(expectedValue, actual);

            KeyValueStore[key] = randomValue;
            actual = KeyValueStore[key];
            Assert.Equal(randomValue, actual);
            Assert.NotEqual(expectedValue, actual);
        }
    }

    [Fact]
    public virtual void Delete()
    {
        foreach (string key in PreStoredDataKeys)
        {
            KeyValueStore.Remove(key);
            Assert.False(KeyValueStore.ContainsKey(key));
        }

        string nonExistent = NewRandomKey();
        KeyValueStore.Remove(nonExistent);
        Assert.False(KeyValueStore.ContainsKey(nonExistent));
    }

    [Fact]
    public virtual void DeleteMany()
    {
        string[] nonExistentKeys = Enumerable.Range(0, 10)
            .Select(_ => NewRandomKey())
            .ToArray();
        string[] keys = PreStoredDataKeys
            .Concat(PreStoredDataKeys.Take(PreStoredDataCount / 2))
            .Concat(nonExistentKeys)
            .ToArray();
        KeyValueStore.RemoveMany(keys);
        Assert.All(keys, k => Assert.False(KeyValueStore.ContainsKey(k)));
    }

    [Fact]
    public void Exists()
    {
        foreach (var (key, _) in PreStoredDataKeys.Zip(PreStoredDataValues, ValueTuple.Create))
        {
            Assert.True(KeyValueStore.ContainsKey(key));
        }

        var randomKey = NewRandomKey();
        Assert.False(KeyValueStore.ContainsKey(randomKey));
    }

    [Fact]
    public void ListKeys()
    {
        ImmutableHashSet<string> keys = KeyValueStore.Keys.ToImmutableHashSet();
        Assert.Equal(PreStoredDataCount, keys.Count);
        Assert.True(PreStoredDataKeys.ToImmutableHashSet().SetEquals(keys));
    }

    public string NewRandomKey()
    {
        return RandomUtility.Word(item => !KeyValueStore.ContainsKey(item));
    }

    protected void InitializePreStoredData()
    {
        PreStoredDataKeys = new string[PreStoredDataCount];
        PreStoredDataValues = new byte[PreStoredDataCount][];

        for (int i = 0; i < PreStoredDataCount; ++i)
        {
            PreStoredDataKeys[i] = RandomUtility.Word();
            PreStoredDataValues[i] = Random.NextBytes(PreStoredDataValueSize);
            KeyValueStore[PreStoredDataKeys[i]] = PreStoredDataValues[i];
        }
    }
}
