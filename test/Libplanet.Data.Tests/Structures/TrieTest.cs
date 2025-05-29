using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Data.Structures;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Types;
using Libplanet.Types.Tests;
using static System.Linq.Enumerable;
using System.Collections;
using Xunit.Abstractions;

namespace Libplanet.Data.Tests.Structures;

public sealed partial class TrieTest(ITestOutputHelper output)
{
    [Fact]
    public void Node()
    {
        var trie1 = new Trie();
        Assert.IsType<NullNode>(trie1.Node);

        var hash = RandomUtility.HashDigest<SHA256>();
        var hashNode = new HashNode { Hash = hash, Table = new MemoryTable() };
        var trie2 = new Trie(hashNode);
        Assert.IsType<HashNode>(trie2.Node);

        var valueNode = new ValueNode { Value = "test" };
        var trie3 = new Trie(valueNode);
        Assert.IsType<ValueNode>(trie3.Node);

        Assert.Equal(trie1, trie1 with { });
    }

    [Fact]
    public void Hash()
    {
        var trie1 = new Trie();
        Assert.Equal(default, trie1.Hash);

        var hash = RandomUtility.HashDigest<SHA256>();
        var hashNode = new HashNode { Hash = hash, Table = new MemoryTable() };
        var trie2 = new Trie(hashNode);
        Assert.Equal(hash, trie2.Hash);

        var valueNode = new ValueNode { Value = "test" };
        var valueHash = HashDigest<SHA256>.Create(ModelSerializer.SerializeToBytes(valueNode));
        var trie3 = new Trie(valueNode);
        Assert.Equal(valueHash, trie3.Hash);
    }

    [Fact]
    public void IsCommitted()
    {
        var trie1 = new Trie();
        Assert.False(trie1.IsCommitted);

        var hash = RandomUtility.HashDigest<SHA256>();
        var hashNode = new HashNode { Hash = hash, Table = new MemoryTable() };
        var trie2 = new Trie(hashNode);
        Assert.True(trie2.IsCommitted);

        var valueNode = new ValueNode { Value = "test" };
        var trie3 = new Trie(valueNode);
        Assert.False(trie3.IsCommitted);
    }

    [Fact]
    public void IsEmpty()
    {
        var trie1 = new Trie();
        Assert.True(trie1.IsEmpty);

        var hash = RandomUtility.HashDigest<SHA256>();
        var hashNode = new HashNode { Hash = hash, Table = new MemoryTable() };
        var trie2 = new Trie(hashNode);
        Assert.False(trie2.IsEmpty);

        var valueNode = new ValueNode { Value = "test" };
        var trie3 = new Trie(valueNode);
        Assert.False(trie3.IsEmpty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Get(bool commit)
    {
        var states = new StateIndex();
        var trie1 = new Trie();
        Assert.Throws<KeyNotFoundException>(() => _ = trie1["nonexistent"]);

        var value = RandomUtility.Word();
        var trie2 = trie1.Set("key", value);
        trie2 = commit ? states.Commit(trie2) : trie2;
        Assert.Equal(value, trie2["key"]);

        var trie3 = trie1.Set(string.Empty, value);
        trie3 = commit ? states.Commit(trie3) : trie3;
        Assert.Equal(value, trie3[string.Empty]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create(bool commit)
    {
        var states = new StateIndex();
        var keyValues = Range(0, 10)
            .Select(i => (RandomUtility.Word(), (object)RandomUtility.Int32()))
            .ToArray();

        var trie = Trie.Create(keyValues);
        trie = commit ? states.Commit(trie) : trie;

        foreach (var (key, value) in keyValues)
        {
            Assert.Equal(value, trie[key]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Set(bool commit)
    {
        var states = new StateIndex();
        var trie1 = new Trie();

        var value = RandomUtility.Word();
        var trie2 = trie1.Set("key", value);
        trie2 = commit ? states.Commit(trie2) : trie2;
        Assert.Equal(value, trie2["key"]);
        Assert.NotEqual(trie1, trie2);
        Assert.NotEqual(trie1.Hash, trie2.Hash);

        var trie3 = trie2.Set(string.Empty, value);
        trie3 = commit ? states.Commit(trie3) : trie3;
        Assert.Equal(value, trie3[string.Empty]);
        Assert.NotEqual(trie2, trie3);
        Assert.NotEqual(trie2.Hash, trie3.Hash);

        var newValue = RandomUtility.Word();
        var trie4 = trie3.Set("key", newValue);
        trie4 = commit ? states.Commit(trie4) : trie4;
        Assert.Equal(newValue, trie4["key"]);
        Assert.NotEqual(trie3, trie4);
        Assert.NotEqual(trie3.Hash, trie4.Hash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Remove(bool commit)
    {
        var seed = RandomUtility.Int32();
        output.WriteLine($"Seed: {seed}");
        var random = new Random(seed);
        var states = new StateIndex();
        var keyValues = Range(0, 10)
            .Select(i => (RandomUtility.Word(random), (object)RandomUtility.Int32(random)))
            .ToArray();

        var trie1 = Trie.Create(keyValues);
        trie1 = commit ? states.Commit(trie1) : trie1;

        var trie2 = trie1.Remove(keyValues[0].Item1);
        trie2 = commit ? states.Commit(trie2) : trie2;
        Assert.Throws<KeyNotFoundException>(() => _ = trie2[keyValues[0].Item1]);
        Assert.NotEqual(trie1, trie2);
        Assert.NotEqual(trie1.Hash, trie2.Hash);

        Assert.Throws<KeyNotFoundException>(() => trie2.Remove("nonexistent"));

        Assert.Throws<InvalidOperationException>(() => new Trie().Remove("nonexistent"));
    }

    [Fact]
    public void GetNode()
    {
        var stateStore = new StateIndex();
        var keyValues = new (string, object)[]
        {
            ("00", "00"),
            ("0000", "0000"),
            ("0010", "00000000000000000000000000000000_0010"),
        };
        var trie1 = Trie.Create(keyValues);
        Assert.IsType<ShortNode>(trie1.GetNode(string.Empty));
        Assert.IsType<FullNode>(trie1.GetNode("00"));
        Assert.Throws<KeyNotFoundException>(() => trie1.GetNode("01"));
        Assert.IsType<ShortNode>(trie1.GetNode("000"));
        Assert.IsType<ShortNode>(trie1.GetNode("001"));
        Assert.IsType<ValueNode>(trie1.GetNode("0000"));
        Assert.IsType<ValueNode>(trie1.GetNode("0010"));

        var trie2 = stateStore.Commit(trie1);
        Assert.IsType<HashNode>(trie2.GetNode(string.Empty));
        Assert.IsType<HashNode>(trie2.GetNode("00"));
        Assert.Throws<KeyNotFoundException>(() => trie2.GetNode("01"));
        Assert.IsType<HashNode>(trie2.GetNode("000"));
        Assert.IsType<HashNode>(trie2.GetNode("001"));
        Assert.IsType<HashNode>(trie2.GetNode("0000"));
        Assert.IsType<HashNode>(trie2.GetNode("0010"));
    }

    [Fact]
    public void TryGetNode()
    {
        var stateStore = new StateIndex();
        var keyValues = new (string, object)[]
        {
            ("00", "00"),
            ("0000", "0000"),
            ("0010", "00000000000000000000000000000000_0010"),
        };
        var trie1 = Trie.Create(keyValues);
        Assert.True(trie1.TryGetNode(string.Empty, out _));
        Assert.True(trie1.TryGetNode("00", out _));
        Assert.False(trie1.TryGetNode("01", out _));
        Assert.True(trie1.TryGetNode("000", out _));
        Assert.True(trie1.TryGetNode("001", out _));
        Assert.True(trie1.TryGetNode("0000", out _));
        Assert.True(trie1.TryGetNode("0010", out _));

        var trie2 = stateStore.Commit(trie1);
        Assert.True(trie2.TryGetNode(string.Empty, out _));
        Assert.True(trie2.TryGetNode("00", out _));
        Assert.False(trie2.TryGetNode("01", out _));
        Assert.True(trie2.TryGetNode("000", out _));
        Assert.True(trie2.TryGetNode("001", out _));
        Assert.True(trie2.TryGetNode("0000", out _));
        Assert.True(trie2.TryGetNode("0010", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryGetValue(bool commit)
    {
        var states = new StateIndex();
        var keyValues = Range(0, 10)
            .Select(i => (RandomUtility.Word(), (object)RandomUtility.Int32()))
            .ToArray();

        var trie1 = Trie.Create(keyValues);
        trie1 = commit ? states.Commit(trie1) : trie1;
        foreach (var (key, value) in keyValues)
        {
            Assert.True(trie1.TryGetValue(key, out var result));
            Assert.Equal(value, result);
        }

        Assert.False(trie1.TryGetValue("nonexistent", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContainsKey(bool commit)
    {
        var states = new StateIndex();
        var keyValues = Range(0, 10)
            .Select(i => (RandomUtility.Word(), (object)RandomUtility.Int32()))
            .ToArray();

        var trie1 = Trie.Create(keyValues);
        trie1 = commit ? states.Commit(trie1) : trie1;
        foreach (var (key, _) in keyValues)
        {
            Assert.True(trie1.ContainsKey(key));
        }

        Assert.False(trie1.ContainsKey("nonexistent"));

        Assert.False(new Trie().ContainsKey("nonexistent"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetEnumerator(bool commit)
    {
        var states = new StateIndex();
        var keyValues = Range(0, 10)
            .Select(i => (RandomUtility.Word(), (object)RandomUtility.Int32()))
            .ToArray();

        var trie1 = Trie.Create(keyValues);
        trie1 = commit ? states.Commit(trie1) : trie1;

        var keyList = new List<string>();
        var valueList = new List<object>();
        var enumerator = trie1.GetEnumerator();
        while (enumerator.MoveNext())
        {
            keyList.Add(enumerator.Current.Key);
            valueList.Add(enumerator.Current.Value);
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
    public void IEnumerable_GetEnumerator(bool commit)
    {
        var states = new StateIndex();
        var keyValues = Range(0, 10)
            .Select(i => (RandomUtility.Word(), (object)RandomUtility.Int32()))
            .ToArray();

        var trie1 = Trie.Create(keyValues);
        trie1 = commit ? states.Commit(trie1) : trie1;

        var keyList = new List<object>();
        var valueList = new List<object>();
        var enumerator = ((IEnumerable)trie1).GetEnumerator();
        while (enumerator.MoveNext())
        {
            var item = enumerator.Current;
            var key = item.GetType().GetProperty("Key")!.GetValue(item)!;
            var value = item.GetType().GetProperty("Value")!.GetValue(item)!;
            keyList.Add(key);
            valueList.Add(value);
        }

        foreach (var (key, value) in keyValues)
        {
            Assert.Contains(key, keyList);
            Assert.Contains(value, valueList);
        }
    }
}
