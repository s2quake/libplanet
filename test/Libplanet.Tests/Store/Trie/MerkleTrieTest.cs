using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Store.Trie.Nodes;
using Xunit;
using static System.Linq.Enumerable;
using static Libplanet.Common.HashDigest<System.Security.Cryptography.SHA256>;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Store.Trie;

public class MerkleTrieTest
{
    [Fact]
    public void ConstructWithHashDigest()
    {
        using var store = new MemoryKeyValueStore();
        var hashDigest = new HashDigest<SHA256>(GetRandomBytes(Size));
        var trie = new Libplanet.Store.Trie.Trie(new HashNode(hashDigest) { KeyValueStore = store });
        Assert.Equal(hashDigest, trie.Hash);
    }

    [Fact]
    public void ConstructWithRootNode()
    {
        using var store = new MemoryKeyValueStore();
        var hashDigest = new HashDigest<SHA256>(GetRandomBytes(Size));
        var node = new HashNode(hashDigest) { KeyValueStore = store };
        var merkleTrie = new Libplanet.Store.Trie.Trie(node);
        Assert.Equal(hashDigest, merkleTrie.Hash);
    }

    [Fact]
    public void CreateWithSingleKeyValue()
    {
        using var store = new MemoryKeyValueStore();
        var keyValue = (KeyBytes.Create([0xbe, 0xef]), Dictionary.Empty);
        var trie = Libplanet.Store.Trie.Trie.Create(keyValue);
        Assert.Single(trie.ToDictionary());
        Assert.Equal(Dictionary.Empty, trie[KeyBytes.Create([0xbe, 0xef])]);
        Assert.Throws<KeyNotFoundException>(() => trie[KeyBytes.Create([0x01])]);

        trie = new TrieStateStore(store).Commit(trie);
        Assert.Single(trie.ToDictionary());
        Assert.Equal(Dictionary.Empty, trie[KeyBytes.Create([0xbe, 0xef])]);
        Assert.Throws<KeyNotFoundException>(() => trie[KeyBytes.Create([0x01])]);
    }

    [Fact]
    public void ToDictionary()
    {
        using var keyValueStore = new MemoryKeyValueStore();
        using var stateStore = new TrieStateStore(keyValueStore);
        var trie = Libplanet.Store.Trie.Trie.Create([
            ([0xbe, 0xef], Dictionary.Empty),
            ([0x01], Null.Value),
            ([0x02], Null.Value),
            ([0x03], Null.Value),
            ([0x04], Null.Value)]);

        var states = trie.ToDictionary();
        Assert.Equal(5, states.Count);
        Assert.Equal(Null.Value, states[KeyBytes.Create([0x01])]);
        Assert.Equal(Null.Value, states[KeyBytes.Create([0x02])]);
        Assert.Equal(Null.Value, states[KeyBytes.Create([0x03])]);
        Assert.Equal(Null.Value, states[KeyBytes.Create([0x04])]);
        Assert.Equal(Dictionary.Empty, states[KeyBytes.Create([0xbe, 0xef])]);

        trie = stateStore.Commit(trie);
        states = trie.ToDictionary();
        Assert.Equal(5, states.Count);
        Assert.Equal(Null.Value, states[KeyBytes.Create([0x01])]);
        Assert.Equal(Null.Value, states[KeyBytes.Create([0x02])]);
        Assert.Equal(Null.Value, states[KeyBytes.Create([0x03])]);
        Assert.Equal(Null.Value, states[KeyBytes.Create([0x04])]);
        Assert.Equal(Dictionary.Empty, states[KeyBytes.Create([0xbe, 0xef])]);
    }

    [Fact]
    public void IterateNodes()
    {
        using var stateStore = new TrieStateStore();
        var trie = Libplanet.Store.Trie.Trie.Create(
            ([0xbe, 0xef], Dictionary.Empty.Add(GetRandomBytes(32), Null.Value)));
        // There are (ShortNode, ValueNode)
        Assert.Equal(2, trie.IterateNodes().Count());

        trie = stateStore.Commit(trie);
        // There are (HashNode, ShortNode, HashNode, ValueNode)
        Assert.Equal(4, trie.IterateNodes().Count());
    }

    [Theory]
    [InlineData(true, "_")]
    [InlineData(false, "_")]
    [InlineData(true, "_1ab3_639e")]
    [InlineData(false, "_1ab3_639e")]
    public void IterateSubTrie(bool commit, string extraKey)
    {
        using var stateStore = new TrieStateStore();
        string[] keys =
        [
            "1b418c98",
            "__3b8a",
            "___",
        ];
        var keyValues = keys
            .Select(key => ((KeyBytes)key, (IValue)new Text(key)))
            .ToArray();
        var trie = Libplanet.Store.Trie.Trie.Create(keyValues);
        var prefixKey = (KeyBytes)"_";

        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.False(trie.TryGetNode(prefixKey, out _));
        Assert.False(trie.TryGetNode(prefixKey, out _));

        trie = trie.Set(extraKey, new Text(extraKey));
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(3, trie.GetNode(prefixKey).SelfAndDescendants().OfType<ValueNode>().Count());
        Assert.Equal(3, trie.GetNode(prefixKey).KeyValues().Count());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Set(bool commit)
    {
        using var stateStore = new TrieStateStore();
        var trie = Libplanet.Store.Trie.Trie.Create(
            ((KeyBytes)"_", Dictionary.Empty));

        Assert.Throws<KeyNotFoundException>(() => trie[[0xbe, 0xef]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0x11, 0x22]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0xaa, 0xbb]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0x12, 0x34]]);

        trie = trie.Set([0xbe, 0xef], Null.Value);
        trie = commit ? stateStore.Commit(trie) : trie;
        AssertBytesEqual(
            Parse("16fc25f43edd0c2d2cb6e3cc3827576e57f4b9e04f8dc3a062c7fe59041f77bd"),
            trie.Hash
        );
        AssertBencodexEqual(Null.Value, trie[[0xbe, 0xef]]);
        Assert.Null(trie[[0x11, 0x22]]);
        Assert.Null(trie[[0xaa, 0xbb]]);
        Assert.Null(trie[[0x12, 0x34]]);

        trie = trie.Set([0xbe, 0xef], new Bencodex.Types.Boolean(true));
        trie = commit ? stateStore.Commit(trie) : trie;
        AssertBytesEqual(
            Parse("4458796f4092b5ebfc1ffb3989e72edee228501e438080a12dea45591dc66d58"),
            trie.Hash
        );
        AssertBencodexEqual(
            new Bencodex.Types.Boolean(true),
            trie[[0xbe, 0xef]]
        );
        Assert.Null(trie[[0x11, 0x22]]);
        Assert.Null(trie[[0xaa, 0xbb]]);
        Assert.Null(trie[[0x12, 0x34]]);

        trie = trie.Set([0x11, 0x22], List.Empty);
        trie = commit ? stateStore.Commit(trie) : trie;
        AssertBytesEqual(
            Parse("ab1359a2497453110a9c658dd3db45f282404fe68d8c8aca30856f395572284c"),
            trie.Hash
        );
        AssertBencodexEqual(
            new Bencodex.Types.Boolean(true),
            trie[[0xbe, 0xef]]
        );
        AssertBencodexEqual(List.Empty, trie[[0x11, 0x22]]);
        Assert.Null(trie[[0xaa, 0xbb]]);
        Assert.Null(trie[[0x12, 0x34]]);

        trie = trie.Set([0xaa, 0xbb], new Text("hello world"));
        trie = commit ? stateStore.Commit(trie) : trie;
        AssertBytesEqual(
            Parse("abb5759141f7af1c40f1b0993ba60073cf4227900617be9641373e5a097eaa3c"),
            trie.Hash
        );
        AssertBencodexEqual(
            new Bencodex.Types.Boolean(true),
            trie[[0xbe, 0xef]]
        );
        AssertBencodexEqual(List.Empty, trie[[0x11, 0x22]]);
        AssertBencodexEqual(
            new Text("hello world"),
            trie[[0xaa, 0xbb]]
        );
        Assert.Null(trie[[0x12, 0x34]]);

        // Once node encoding length exceeds certain length,
        // uncommitted and committed hash diverge
        var longText = new Text(string.Join("\n", Range(0, 1000).Select(i => $"long str {i}")));
        trie = trie.Set([0xaa, 0xbb], longText);
        trie = commit ? stateStore.Commit(trie) : trie;
        AssertBytesEqual(
            Parse(commit
                ? "56e5a39a726acba1f7631a6520ae92e20bb93ca3992a7b7d3542c6daee68e56d"
                : "ad9fb53a8f643bd308d7afea57a5d1796d6031b1df95bdd415fa69b44177d155"),
            trie.Hash
        );
        AssertBencodexEqual(
            new Bencodex.Types.Boolean(true),
            trie[[0xbe, 0xef]]
        );
        AssertBencodexEqual(List.Empty, trie[[0x11, 0x22]]);
        AssertBencodexEqual(longText, trie[[0xaa, 0xbb]]);
        Assert.Null(trie[[0x12, 0x34]]);

        trie = trie.Set([0x12, 0x34], Dictionary.Empty);
        trie = commit ? stateStore.Commit(trie) : trie;
        AssertBytesEqual(
            Parse(commit
                ? "88d6375097fd03e6c30a129eb0030d938caeaa796643971ca938fbd27ff5e057"
                : "77d13e9d97033400ad31fcb0441819285b9165f6ea6ae599d85e7d7e24428feb"),
            trie.Hash
        );
        AssertBencodexEqual(
            new Bencodex.Types.Boolean(true),
            trie[[0xbe, 0xef]]
        );
        AssertBencodexEqual(List.Empty, trie[[0x11, 0x22]]);
        AssertBencodexEqual(longText, trie[[0xaa, 0xbb]]);
        AssertBencodexEqual(Dictionary.Empty, trie[[0x12, 0x34]]);

        List complexList = List.Empty
            .Add("Hello world")
            .Add(Dictionary.Empty
                .Add("foo", 1)
                .Add("bar", 2)
                .Add(
                    "lst",
                    new List(Range(0, 1000).Select(i => new Text($"long str {i}")))))
            .Add(new List(Range(0, 1000).Select(i => new Text($"long str {i}"))));
        trie = trie.Set([0x11, 0x22], complexList);
        trie = commit ? stateStore.Commit(trie) : trie;
        AssertBytesEqual(
            Parse(commit
                ? "f29820df65c1d1a66b69a59b9fe3e21911bbd2d97a9f298853c529804bf84a26"
                : "586ba0ba5dfe07433b01fbf7611f95832bde07b8dc5669540ef8866f465bbb85"),
            trie.Hash
        );
        AssertBencodexEqual(
            new Bencodex.Types.Boolean(true),
            trie[[0xbe, 0xef]]
        );
        AssertBencodexEqual(complexList, trie[[0x11, 0x22]]);
        AssertBencodexEqual(longText, trie[[0xaa, 0xbb]]);
        AssertBencodexEqual(Dictionary.Empty, trie[[0x12, 0x34]]);

        Dictionary complexDict = Dictionary.Empty
            .Add("foo", 123)
            .Add("bar", 456)
            .Add("lst", new List(Range(0, 1000).Select(i => new Text($"long str {i}"))))
            .Add("cls", complexList)
            .Add(
                "dct",
                Dictionary.Empty
                    .Add("abcd", Null.Value)
                    .Add("efgh", false)
                    .Add("ijkl", true)
                    .Add("mnop", new Binary("hello world", Encoding.ASCII))
                    .Add("qrst", complexList)
                    .Add("uvwx", Dictionary.Empty));
        trie = trie.Set([0x12, 0x34], complexDict);
        trie = commit ? stateStore.Commit(trie) : trie;
        AssertBytesEqual(
            Parse(commit
                ? "1dabec2c0fea02af0182e9fee6c7ce7ad1a9d9bcfaa2cd80c2971bbce5272655"
                : "4783d18dfc8a2d4d98f722a935e45bd7fc1d0197fb4d33e62f734bfde968af39"),
            trie.Hash
        );
        AssertBencodexEqual(
            new Bencodex.Types.Boolean(true),
            trie[[0xbe, 0xef]]
        );
        AssertBencodexEqual(complexList, trie[[0x11, 0x22]]);
        AssertBencodexEqual(longText, trie[[0xaa, 0xbb]]);
        AssertBencodexEqual(complexDict, trie[[0x12, 0x34]]);
    }

    [Fact]
    public void GetNode()
    {
        using var stateStore = new TrieStateStore();
        var keyValues = new (ImmutableArray<byte>, IValue)[]
        {
            ([0x00], new Text("00")),
            ([0x00, 0x00], new Text("0000")),
            ([0x00, 0x10], new Text("00000000000000000000000000000000_0010")),
        };
        var trie1 = Libplanet.Store.Trie.Trie.Create(keyValues);

        Assert.IsType<ShortNode>(trie1.GetNode(Nibbles.Parse(string.Empty)));
        Assert.IsType<FullNode>(trie1.GetNode(Nibbles.Parse("00")));
        Assert.Throws<KeyNotFoundException>(() => trie1.GetNode(Nibbles.Parse("01")));
        Assert.IsType<ShortNode>(trie1.GetNode(Nibbles.Parse("000")));
        Assert.IsType<ShortNode>(trie1.GetNode(Nibbles.Parse("001")));
        Assert.IsType<ValueNode>(trie1.GetNode(Nibbles.Parse("0000")));
        Assert.IsType<ValueNode>(trie1.GetNode(Nibbles.Parse("0010")));

        var trie2 = stateStore.Commit(trie1);
        Assert.IsType<HashNode>(trie2.GetNode(Nibbles.Parse(string.Empty)));
        Assert.IsType<HashNode>(trie2.GetNode(Nibbles.Parse("00")));
        Assert.Throws<KeyNotFoundException>(() => trie2.GetNode(Nibbles.Parse("01")));
        Assert.IsType<ShortNode>(trie2.GetNode(Nibbles.Parse("000")));
        Assert.IsType<HashNode>(trie2.GetNode(Nibbles.Parse("001")));
        Assert.IsType<ValueNode>(trie2.GetNode(Nibbles.Parse("0000")));
        Assert.IsType<HashNode>(trie2.GetNode(Nibbles.Parse("0010")));
    }

    [Fact]
    public void ResolveToValueAtTheEndOfShortNode()
    {
        using var stateStore = new TrieStateStore();
        var trie = Libplanet.Store.Trie.Trie.Create(
            (Key: [0x00], Value: new Text("00")));

        trie = stateStore.Commit(trie);

        Assert.Throws<KeyNotFoundException>(() => trie[key: [0x00, 0x00]]);
    }

    [Fact]
    public void SetValueToExtendedKey()
    {
        using var stateStore = new TrieStateStore();
        var value00 = new Text("00");
        var value0000 = new Text("0000");
        var trie = Libplanet.Store.Trie.Trie.Create(
            (Key: [0x00], Value: value00),
            (Key: [0x00, 0x00], Value: value0000));

        trie = stateStore.Commit(trie);

        Assert.Equal(2, trie.ToDictionary().Count);
        Assert.Equal(value00, trie[[0x00]]);
        Assert.Equal(value0000, trie[[0x00, 0x00]]);
    }

    [Fact]
    public void SetValueToFullNode()
    {
        var stateStore = new TrieStateStore();
        var value00 = new Text("00");
        var value0000 = new Text("0000");
        var value0010 = new Text("0010");
        var trie = Libplanet.Store.Trie.Trie.Create(
            (Key: [0x00], Value: value00),
            (Key: [0x00, 0x00], Value: value0000),
            (Key: [0x00, 0x10], Value: value0010));

        trie = stateStore.Commit(trie);

        Assert.Equal(3, trie.ToDictionary().Count);
        Assert.Equal(value00, trie[[0x00]]);
        Assert.Equal(value0000, trie[[0x00, 0x00]]);
        Assert.Equal(value0010, trie[[0x00, 0x10]]);
    }

    [Fact]
    public void RemoveValue()
    {
        var stateStore = new TrieStateStore();
        var key00 = KeyBytes.Create([0x00]);
        var value00 = new Text("00");
        var key0000 = KeyBytes.Create([0x00, 0x00]);
        var value0000 = new Text("0000");

        var trie = Libplanet.Store.Trie.Trie.Create(
            (Key: key00, Value: value00));
        trie = stateStore.Commit(trie);
        Assert.Null(trie.Remove(key00));

        trie = Libplanet.Store.Trie.Trie.Create(
            (Key: key0000, Value: value0000));
        trie = stateStore.Commit(trie);
        int expectedNodeCount = trie.IterateNodes().Count();
        int expectedValueCount = trie.ToDictionary().Count;
        HashDigest<SHA256> expectedHash = trie.Hash;

        trie = Libplanet.Store.Trie.Trie.Create(
            (Key: key00, Value: value00),
            (Key: key0000, Value: value0000));
        trie = stateStore.Commit(trie);
        trie = trie.Remove(key00);
        trie = stateStore.Commit(trie);
        Assert.Equal(value0000, trie[[0x00, 0x00]]);
        Assert.Equal(expectedNodeCount, trie.IterateNodes().Count());
        Assert.Equal(expectedValueCount, trie.ToDictionary().Count);
        Assert.Equal(expectedHash, trie.Hash);

        trie = Libplanet.Store.Trie.Trie.Create(
            (Key: key00, Value: value00));
        trie = stateStore.Commit(trie);
        expectedNodeCount = trie.IterateNodes().Count();
        expectedValueCount = trie.ToDictionary().Count;
        expectedHash = trie.Hash;

        trie = Libplanet.Store.Trie.Trie.Create(
            (Key: key00, Value: value00),
            (Key: key0000, Value: value0000));
        trie = stateStore.Commit(trie);
        trie = trie.Remove(key0000);
        trie = stateStore.Commit(trie);
        Assert.Equal(value00, Assert.Single(trie.ToDictionary()).Value);
        Assert.Equal(expectedNodeCount, trie.IterateNodes().Count());
        Assert.Equal(expectedValueCount, trie.ToDictionary().Count);
        Assert.Equal(expectedHash, trie.Hash);

        trie = Libplanet.Store.Trie.Trie.Create(
            (Key: key00, Value: value00),
            (Key: key0000, Value: value0000));
        trie = stateStore.Commit(trie);
        HashDigest<SHA256> hash = trie.Hash; // A reference to an earlier point in time.
        trie = trie.Remove(key00);
        Assert.Null(trie.Remove(key0000));

        trie = stateStore.GetStateRoot(hash);
        Assert.Equal(value00, trie[[0x00]]); // Nothing is actually removed from storage.
        Assert.Equal(value0000, trie[[0x00, 0x00]]);

        // Add randomized kvs and remove kvs in order.
        // The way the test is set up, identical kv pairs shouldn't matter.
        Random random = new Random();
        List<(KeyBytes Key, Text Value)> kvs = Enumerable
            .Range(0, 100)
            .Select(_ => TestUtils.GetRandomBytes(random.Next(2, 10)))
            .Select(bytes => (KeyBytes.Create(bytes), new Text(ByteUtil.Hex(bytes))))
            .ToList();
        var expected = new Stack<(HashDigest<SHA256>, int, int)>();

        for (var i = 0; i < kvs.Count; i++)
        {
            var kv = kvs[i];
            trie = i == 0 ? Libplanet.Store.Trie.Trie.Create(kv) : trie.Set(kv.Key, kv.Value);
            trie = stateStore.Commit(trie);
            expected.Push(
                (trie.Hash, trie.IterateNodes().Count(), trie.Count()));
        }

        for (var i = kvs.Count - 1; i >= 0; i--)
        {
            var (key, value) = kvs[i];
            var tuple = expected.Pop();
            Assert.Equal(tuple.Item3, trie.Count());
            Assert.Equal(tuple.Item2, trie.IterateNodes().Count());
            Assert.Equal(tuple.Item1, trie.Hash);
            trie = trie.Remove(key);
            trie = trie is not null ? stateStore.Commit(trie) : null;
        }

        Assert.Empty(expected);
        Assert.Null(trie);
    }

    [Fact]
    public void RemoveValueNoOp()
    {
        var stateStore = new TrieStateStore();
        var key00 = KeyBytes.Create([0x00]);
        var key0000 = KeyBytes.Create([0x00, 0x00]);
        var value0000 = new Text("0000");
        var key0011 = KeyBytes.Create([0x00, 0x11]);
        var value0011 = new Text("0011");
        var key000000 = KeyBytes.Create([0x00, 0x00, 0x00]);
        var trie = Libplanet.Store.Trie.Trie.Create(
            (Key: key0000, Value: value0000),
            (Key: key0011, Value: value0011));
        trie = stateStore.Commit(trie);
        int expectedNodeCount = trie.IterateNodes().Count();
        int expectedValueCount = trie.ToDictionary().Count;
        HashDigest<SHA256> expectedHash = trie.Hash;

        trie = trie.Remove(key00);
        trie = trie.Remove(key000000);
        trie = stateStore.Commit(trie);
        Assert.Equal(expectedNodeCount, trie.IterateNodes().Count());
        Assert.Equal(expectedValueCount, trie.Count());
        Assert.Equal(expectedHash, trie.Hash);
    }
}
