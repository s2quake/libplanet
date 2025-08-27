using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Data.Structures;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Types;
using Libplanet.TestUtilities;
using static System.Linq.Enumerable;

namespace Libplanet.Data.Tests.Structures;

public sealed partial class TrieTest
{
    [Fact]
    public void ConstructWithHashDigest()
    {
        var store = new MemoryTable();
        var hashDigest = RandomUtility.HashDigest<SHA256>();
        var trie = new Trie(new HashNode { Hash = hashDigest, Table = store });
        Assert.Equal(hashDigest, trie.Hash);
    }

    [Fact]
    public void ConstructWithRootNode()
    {
        var store = new MemoryTable();
        var hashDigest = RandomUtility.HashDigest<SHA256>();
        var node = new HashNode { Hash = hashDigest, Table = store };
        var trie = new Trie(node);
        Assert.Equal(hashDigest, trie.Hash);
    }

    [Fact]
    public void CreateWithSingleKeyValue()
    {
        var stateStore = new StateIndex();
        var keyValue = ("01", ImmutableSortedDictionary<string, string>.Empty);
        var trie = Trie.Create(keyValue);
        Assert.Single(trie.ToDictionary());
        Assert.Equal(ImmutableSortedDictionary<string, string>.Empty, trie["01"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0"]);

        trie = stateStore.Commit(trie);
        Assert.Single(trie.ToDictionary());
        Assert.Equal(ImmutableSortedDictionary<string, string>.Empty, trie["01"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0"]);
    }

    [Fact]
    public void ToDictionary()
    {
        var keyValueStore = new MemoryTable();
        var stateStore = new StateIndex(keyValueStore);
        var trie = new Trie()
            .Set("00", ImmutableSortedDictionary<string, string>.Empty)
            .Set("1", "1")
            .Set("2", "2")
            .Set("3", "3")
            .Set("4", "4");

        var states = trie.ToDictionary();
        Assert.Equal(5, states.Count);
        Assert.Equal("1", states["1"]);
        Assert.Equal("2", states["2"]);
        Assert.Equal("3", states["3"]);
        Assert.Equal("4", states["4"]);
        Assert.Equal(ImmutableSortedDictionary<string, string>.Empty, states["00"]);

        trie = stateStore.Commit(trie);
        states = trie.ToDictionary();
        Assert.Equal(5, states.Count);
        Assert.Equal("1", states["1"]);
        Assert.Equal("2", states["2"]);
        Assert.Equal("3", states["3"]);
        Assert.Equal("4", states["4"]);
        Assert.Equal(ImmutableSortedDictionary<string, string>.Empty, states["00"]);
    }

    [Fact]
    public void IterateNodes()
    {
        var stateStore = new StateIndex();
        var trie = Trie.Create(
            ((string Key, object Value))("ab", ImmutableSortedDictionary<string, string>.Empty.Add("a", "b")));

        // There are (ShortNode, ValueNode)
        Assert.Equal(2, trie.Node.Traverse().Count());

        trie = stateStore.Commit(trie);

        // There are (HashNode, ShortNode, HashNode, ValueNode)
        Assert.Equal(4, trie.Node.Traverse().Count());
    }

    [Theory]
    [InlineData(true, "_")]
    [InlineData(false, "_")]
    [InlineData(true, "_1ab3_639e")]
    [InlineData(false, "_1ab3_639e")]
    public void IterateSubTrie(bool commit, string extraKey)
    {
        var stateStore = new StateIndex();
        string[] keys =
        [
            "1b418c98",
            "__3b8a",
            "___",
        ];
        var keyValues = keys
            .Select(key => (key, (object)key))
            .ToArray();
        var trie = Trie.Create(keyValues);
        var prefixKey = "_";

        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(2, trie.GetNode(prefixKey).Traverse().OfType<ValueNode>().Count());
        Assert.Equal(2, trie.GetNode(prefixKey).KeyValues().Count());

        trie = trie.Set(extraKey, extraKey);
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(3, trie.GetNode(prefixKey).Traverse().OfType<ValueNode>().Count());
        Assert.Equal(3, trie.GetNode(prefixKey).KeyValues().Count());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetTheory(bool commit)
    {
        var stateStore = new StateIndex();
        var trie = Trie.Create(
            ((string Key, object Value))("_", ImmutableSortedDictionary<string, string>.Empty));

        Assert.Throws<KeyNotFoundException>(() => trie["0xbe, 0xef"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x11, 0x22"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0xbe, 0xef", "null");
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "c7fe6d6e894c80a1d4194d5d8a902ed863c9fd9381ac9f3ad1c63bbf9d33107d"
                : "5093c6e77083a3a0b3eaa4a4d31c32e3c7c82e5c3876436275473aaaa8b748d4"),
            trie.Hash);
        Assert.Equal("null", trie["0xbe, 0xef"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x11, 0x22"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0xbe, 0xef", true);
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "fa3b2fdfc616b95d0d1b80f321e697940697a82e0850feb413c840e101803499"
                : "cc940724c3c13c75ad3388a1fe1ef9c004d664712151948b875414af2910c311"),
            trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Throws<KeyNotFoundException>(() => trie["0x11, 0x22"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0x11, 0x22", new List<string>());
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "a2c8f378e7490f37f76b043c7f14f040b6e1cd9488745d4a0b0eaa231e8dc377"
                : "a99503b54c35e41d0dd09e858d343cf903509ad2579e4c165703a4eb5445ccb5"),
            trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Equal<string>([], (List<string>)trie["0x11, 0x22"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0xaa, 0xbb", "hello world");
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "f8cb0be36caff8f37f9cc42cafc506f1d588989135c9f31edcc26aeb03eb1aaa"
                : "169a07a710939e6cd8f6536abd02878203e5802e3cdd925d00bbdeca1b69021f"),
            trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Equal<string>([], (List<string>)trie["0x11, 0x22"]);
        Assert.Equal("hello world", trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        // Once node encoding length exceeds certain length,
        // uncommitted and committed hash diverge
        var longText = string.Join("\n", Range(0, 1000).Select(i => $"long str {i}"));
        trie = trie.Set("0xaa, 0xbb", longText);
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "0d585620490c8d0eaad8eaf098d602b57b05694fb5d14780570bbca3b593b248"
                : "6fd4b3437f49dddbda2d01f9a11e2e9e82b171c6ee6eef2ba4052eac5b0dadd2"),
            trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Equal<string>([], (List<string>)trie["0x11, 0x22"]);
        Assert.Equal(longText, trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0x12, 0x34", ImmutableSortedDictionary<string, string>.Empty);
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "5c291774004e27591a2f7f356e8a64649ab0f7d790c97472e5a2f3c197ac0391"
                : "f4ad1c355458408b68bb1ee8953a49081b0807ed68e7930cbd3f62d04072b570"),
            trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Equal<string>([], (List<string>)trie["0x11, 0x22"]);
        Assert.Equal(longText, trie["0xaa, 0xbb"]);
        Assert.Equal(ImmutableSortedDictionary<string, string>.Empty, trie["0x12, 0x34"]);

        var complexList = ImmutableList<object>.Empty
            .Add("Hello world")
            .Add(ImmutableSortedDictionary<string, object>.Empty
                .Add("foo", 1)
                .Add("bar", 2)
                .Add(
                    "lst",
                    new List<string>(Range(0, 1000).Select(i => $"long str {i}"))))
            .Add(new List<string>(Range(0, 1000).Select(i => $"long str {i}")));
        trie = trie.Set("0x11, 0x22", complexList);
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "2b5a88e37d74a2bf981b868c9482b227c2674e3b3583b705d04f375beebbff66"
                : "b2d0c79ff0d063d9fcc14959ca9e5eddebcf46d737585f8784beb65f6942588c"),
            trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.True(ModelResolver.Equals(complexList, (ImmutableList<object>)trie["0x11, 0x22"]));
        Assert.Equal(longText, trie["0xaa, 0xbb"]);
        Assert.Equal(ImmutableSortedDictionary<string, string>.Empty, trie["0x12, 0x34"]);

        var complexDict = ImmutableSortedDictionary<string, object>.Empty
            .Add("foo", 123)
            .Add("bar", 456)
            .Add("lst", new List<string>(Range(0, 1000).Select(i => $"long str {i}")))
            .Add("cls", complexList)
            .Add(
                "dct",
                ImmutableSortedDictionary<string, object?>.Empty
                    .Add("abcd", null)
                    .Add("efgh", false)
                    .Add("ijkl", true)
                    .Add("mnop", "hello world")
                    .Add("qrst", complexList)
                    .Add("uvwx", ImmutableSortedDictionary<string, object?>.Empty));
        trie = trie.Set("0x12, 0x34", complexDict);
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "e09d526b2ccb6acf2cd651729ea8fb56b8229376de8ff9ad90f1a32780ab910d"
                : "e9199e0f481b88d31af17c432b12b66bdb87e08c0f56870ab9a77d51b8d2736a"),
            trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Equal(complexList, trie["0x11, 0x22"]);
        Assert.Equal(longText, trie["0xaa, 0xbb"]);
        Assert.Equal(complexDict, trie["0x12, 0x34"]);
    }

    [Fact]
    public void ResolveToValueAtTheEndOfShortNode()
    {
        var stateStore = new StateIndex();
        var trie = Trie.Create(
            (Key: "0x00", Value: "00"));

        trie = stateStore.Commit(trie);

        Assert.Throws<KeyNotFoundException>(() => trie[key: "0x00, 0x00"]);
    }

    [Fact]
    public void SetValueToExtendedKey()
    {
        var stateStore = new StateIndex();
        var value00 = "00";
        var value0000 = "0000";
        var trie = Trie.Create(
            (Key: "0x00", Value: value00),
            (Key: "0x00, 0x00", Value: value0000));

        trie = stateStore.Commit(trie);

        Assert.Equal(2, trie.ToDictionary().Count);
        Assert.Equal(value00, trie["0x00"]);
        Assert.Equal(value0000, trie["0x00, 0x00"]);
    }

    [Fact]
    public void SetValueToFullNode()
    {
        var stateStore = new StateIndex();
        var value00 = "00";
        var value0000 = "0000";
        var value0010 = "0010";
        var trie = Trie.Create(
            (Key: "0x00", Value: value00),
            (Key: "0x00, 0x00", Value: value0000),
            (Key: "0x00, 0x10", Value: value0010));

        trie = stateStore.Commit(trie);

        Assert.Equal(3, trie.ToDictionary().Count);
        Assert.Equal(value00, trie["0x00"]);
        Assert.Equal(value0000, trie["0x00, 0x00"]);
        Assert.Equal(value0010, trie["0x00, 0x10"]);
    }

    [Fact]
    public void RemoveValue()
    {
        var stateStore = new StateIndex();
        var trie = Trie.Create(
            (Key: "0000", Value: "0000"),
            (Key: "0011", Value: "0011"));
        trie = stateStore.Commit(trie);

        int expectedNodeCount = trie.Node.Traverse().Count();
        int expectedValueCount = trie.Count();
        HashDigest<SHA256> expectedHash = trie.Hash;

        trie = trie.Set("1234", "1234");
        trie = stateStore.Commit(trie);
        trie = trie.Remove("1234");
        trie = stateStore.Commit(trie);

        Assert.Equal(expectedNodeCount, trie.Node.Traverse().Count());
        Assert.Equal(expectedValueCount, trie.Count());
        Assert.Equal(expectedHash, trie.Hash);

        trie = trie.Remove("0000");
        trie = trie.Remove("0011");
        trie = trie.IsEmpty ? trie : stateStore.Commit(trie);
        Assert.True(trie.IsEmpty);
    }

    [Fact]
    public void RemoveValueMany()
    {
        var stateStore = new StateIndex();
        var key00 = "00";
        var value00 = "00";
        var key0000 = "0000";
        var value0000 = "0000";

        var trie = new Trie()
            .Set(key00, value00);
        trie = stateStore.Commit(trie);
        Assert.Equal(default, trie.Remove(key00).Hash);

        trie = Trie.Create(
            (Key: key0000, Value: value0000));
        trie = stateStore.Commit(trie);
        int expectedNodeCount = trie.Node.Traverse().Count();
        int expectedValueCount = trie.ToDictionary().Count;
        HashDigest<SHA256> expectedHash = trie.Hash;

        trie = Trie.Create(
            (Key: key00, Value: value00),
            (Key: key0000, Value: value0000));
        trie = stateStore.Commit(trie);
        trie = trie.Remove(key00);
        trie = stateStore.Commit(trie);
        Assert.Equal(value0000, trie["0000"]);
        Assert.Equal(expectedNodeCount, trie.Node.Traverse().Count());
        Assert.Equal(expectedValueCount, trie.ToDictionary().Count);
        Assert.Equal(expectedHash, trie.Hash);

        trie = Trie.Create(
            (Key: key00, Value: value00));
        trie = stateStore.Commit(trie);
        expectedNodeCount = trie.Node.Traverse().Count();
        expectedValueCount = trie.ToDictionary().Count;
        expectedHash = trie.Hash;

        trie = Trie.Create(
            (Key: key00, Value: value00),
            (Key: key0000, Value: value0000));
        trie = stateStore.Commit(trie);
        trie = trie.Remove(key0000);
        trie = stateStore.Commit(trie);
        Assert.Equal(value00, Assert.Single(trie.ToDictionary()).Value);
        Assert.Equal(expectedNodeCount, trie.Node.Traverse().Count());
        Assert.Equal(expectedValueCount, trie.ToDictionary().Count);
        Assert.Equal(expectedHash, trie.Hash);

        trie = Trie.Create(
            (Key: key00, Value: value00),
            (Key: key0000, Value: value0000));
        trie = stateStore.Commit(trie);
        HashDigest<SHA256> hash = trie.Hash; // A reference to an earlier point in time.
        trie = trie.Remove(key00);
        Assert.Equal(default, trie.Remove(key0000).Hash);

        trie = stateStore.GetTrie(hash);
        Assert.Equal(value00, trie["00"]); // Nothing is actually removed from storage.
        Assert.Equal(value0000, trie["0000"]);

        // Add randomized kvs and remove kvs in order.
        // The way the test is set up, identical kv pairs shouldn't matter.
        var kvs = RandomUtility.HashSet(RandomUtility.Word, 100)
            .ToDictionary(item => item, item => item)
            .Select(item => (item.Key, item.Value))
            .ToArray();
        var expected = new Stack<(HashDigest<SHA256> Hash, int NodeCount, int ValueCount)>();

        trie = new Trie();
        for (var i = 0; i < kvs.Length; i++)
        {
            var (k, v) = kvs[i];
            trie = trie.Set(k, v);
            trie = stateStore.Commit(trie);
            expected.Push(
                (trie.Hash, trie.Node.Traverse().Count(), trie.Count()));
        }

        for (var i = kvs.Length - 1; i >= 0; i--)
        {
            var k = kvs[i].Key;
            var (Hash, NodeCount, ValueCount) = expected.Pop();
            Assert.Equal(Hash, trie.Hash);
            Assert.Equal(NodeCount, trie.Node.Traverse().Count());
            Assert.Equal(ValueCount, trie.Count());
            trie = trie.Remove(k);
            trie = trie.IsEmpty ? trie : stateStore.Commit(trie);
        }

        Assert.Empty(expected);
        Assert.True(trie.IsEmpty);
    }

    [Fact]
    public void RemoveValueNoOp()
    {
        var stateStore = new StateIndex();
        var trie = Trie.Create(
            (Key: "0000", Value: "0000"),
            (Key: "0011", Value: "0011"));
        trie = stateStore.Commit(trie);
        int expectedNodeCount = trie.Node.Traverse().Count();
        int expectedValueCount = trie.ToDictionary().Count;
        HashDigest<SHA256> expectedHash = trie.Hash;

        trie = trie.Remove("00");
        trie = trie.Remove("000000");
        trie = stateStore.Commit(trie);
        Assert.Equal(expectedNodeCount, trie.Node.Traverse().Count());
        Assert.Equal(expectedValueCount, trie.Count());
        Assert.Equal(expectedHash, trie.Hash);
    }
}
