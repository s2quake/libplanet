using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Data;
using Libplanet.Data.Structures;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Types;
using Libplanet.Types.Tests;
using static System.Linq.Enumerable;

namespace Libplanet.Data.Tests.Structures;

public class TrieTest
{
    [Fact]
    public void Base_Test()
    {
        var trie = new Trie();
        Assert.Equal(default, trie.Hash);
        Assert.IsType<NullNode>(trie.Node);
    }

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
    public void Set(bool commit)
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
        // Assert.Equal(
        //     Parse("3b6414adc582fcc2d44c0f85be521aad6a98b88d5b685006eb4b37ca314df23d"),
        //     trie.Hash);
        Assert.Equal("null", trie["0xbe, 0xef"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x11, 0x22"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0xbe, 0xef", true);
        trie = commit ? stateStore.Commit(trie) : trie;
        // Assert.Equal(
        //     Parse("5af9a61e8f0d48e4f76b920ae0a279008dfce6abb1c99fa8dfbd23b723949ed4"),
        //     trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Throws<KeyNotFoundException>(() => trie["0x11, 0x22"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0x11, 0x22", new List<string>());
        trie = commit ? stateStore.Commit(trie) : trie;
        // Assert.Equal(
        //     Parse("129d5b1ce5ff32577ac015678388984a0ffbd1beb5a38dac9880ceed9de50731"),
        //     trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Equal<string>([], (List<string>)trie["0x11, 0x22"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0xaa, 0xbb", "hello world");
        trie = commit ? stateStore.Commit(trie) : trie;
        // Assert.Equal(
        //     Parse("7f3e9047e58bfa31edcf4bf3053de808565f0673063fa80c3442b791635a33b3"),
        //     trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Equal<string>([], (List<string>)trie["0x11, 0x22"]);
        Assert.Equal("hello world", trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        // Once node encoding length exceeds certain length,
        // uncommitted and committed hash diverge
        var longText = string.Join("\n", Range(0, 1000).Select(i => $"long str {i}"));
        trie = trie.Set("0xaa, 0xbb", longText);
        trie = commit ? stateStore.Commit(trie) : trie;
        // Assert.Equal(
        //     Parse(commit
        //         ? "56e5a39a726acba1f7631a6520ae92e20bb93ca3992a7b7d3542c6daee68e56d"
        //         : "200481e87f2cc1c0729beb4526de7c54e065e0892e58667e0cbd530b85c4e728"),
        //     trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Equal<string>([], (List<string>)trie["0x11, 0x22"]);
        Assert.Equal(longText, trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0x12, 0x34", ImmutableSortedDictionary<string, string>.Empty);
        trie = commit ? stateStore.Commit(trie) : trie;
        // Assert.Equal(
        //     Parse(commit
        //         ? "88d6375097fd03e6c30a129eb0030d938caeaa796643971ca938fbd27ff5e057"
        //         : "18532d2ee8484a65b102668715c97decf1a3218b23bfb11933748018179cb5cf"),
        //     trie.Hash);
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
        // Assert.Equal(
        //     Parse(commit
        //         ? "f29820df65c1d1a66b69a59b9fe3e21911bbd2d97a9f298853c529804bf84a26"
        //         : "408037f213067c016c09466e75edcb80b2ad5de738be376ee80a364b4cab575a"),
        //     trie.Hash);
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
        // Assert.Equal(
        //     Parse(commit
        //         ? "1dabec2c0fea02af0182e9fee6c7ce7ad1a9d9bcfaa2cd80c2971bbce5272655"
        //         : "4783d18dfc8a2d4d98f722a935e45bd7fc1d0197fb4d33e62f734bfde968af39"),
        //     trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Equal(complexList, trie["0x11, 0x22"]);
        Assert.Equal(longText, trie["0xaa, 0xbb"]);
        Assert.Equal(complexDict, trie["0x12, 0x34"]);
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
        var kvs =
            Range(0, 100)
            .Select(_ => RandomUtility.Word())
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
