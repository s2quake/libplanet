using System.Security.Cryptography;
using System.Text;
using Libplanet.Serialization;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Store.Trie.Nodes;
using Libplanet.Types;
using static System.Linq.Enumerable;
using static Libplanet.Tests.TestUtils;
using static Libplanet.Types.HashDigest<System.Security.Cryptography.SHA256>;

namespace Libplanet.Tests.Store.Trie;

public class MerkleTrieTest
{
    [Fact]
    public void Base_Test()
    {
        var trie = new Libplanet.Store.Trie.Trie();
        Assert.Equal(default, trie.Hash);
        Assert.IsType<NullNode>(trie.Node);
    }

    [Fact]
    public void ConstructWithHashDigest()
    {
        var store = new MemoryTable();
        var hashDigest = RandomUtility.NextHashDigest<SHA256>();
        var trie = new Libplanet.Store.Trie.Trie(new HashNode { Hash = hashDigest, Table = store });
        Assert.Equal(hashDigest, trie.Hash);
    }

    [Fact]
    public void ConstructWithRootNode()
    {
        var store = new MemoryTable();
        var hashDigest = RandomUtility.NextHashDigest<SHA256>();
        var node = new HashNode { Hash = hashDigest, Table = store };
        var trie = new Libplanet.Store.Trie.Trie(node);
        Assert.Equal(hashDigest, trie.Hash);
    }

    [Fact]
    public void CreateWithSingleKeyValue()
    {
        var store = new MemoryTable();
        var keyValue = (new KeyBytes([0xbe, 0xef]), ImmutableSortedDictionary<string, string>.Empty);
        var trie = Libplanet.Store.Trie.Trie.Create(keyValue);
        Assert.Single(trie.ToDictionary());
        Assert.Equal(ImmutableSortedDictionary<string, string>.Empty, trie[new KeyBytes([0xbe, 0xef])]);
        Assert.Throws<KeyNotFoundException>(() => trie[new KeyBytes([0x01])]);

        trie = new TrieStateStore(store).Commit(trie);
        Assert.Single(trie.ToDictionary());
        Assert.Equal(ImmutableSortedDictionary<string, string>.Empty, trie[new KeyBytes([0xbe, 0xef])]);
        Assert.Throws<KeyNotFoundException>(() => trie[new KeyBytes([0x01])]);
    }

    [Fact]
    public void ToDictionary()
    {
        var keyValueStore = new MemoryTable();
        var stateStore = new TrieStateStore(keyValueStore);
        var trie = Libplanet.Store.Trie.Trie.Create(
            ([0xbe, 0xef], ImmutableSortedDictionary<string, string>.Empty),
            ([0x01], "1"),
            ([0x02], "2"),
            ([0x03], "3"),
            ([0x04], "4"));

        var states = trie.ToDictionary();
        Assert.Equal(5, states.Count);
        Assert.Equal("1", states[new KeyBytes([0x01])]);
        Assert.Equal("2", states[new KeyBytes([0x02])]);
        Assert.Equal("3", states[new KeyBytes([0x03])]);
        Assert.Equal("4", states[new KeyBytes([0x04])]);
        Assert.Equal(ImmutableSortedDictionary<string, string>.Empty, states[new KeyBytes([0xbe, 0xef])]);

        trie = stateStore.Commit(trie);
        states = trie.ToDictionary();
        Assert.Equal(5, states.Count);
        Assert.Equal("1", states[new KeyBytes([0x01])]);
        Assert.Equal("2", states[new KeyBytes([0x02])]);
        Assert.Equal("3", states[new KeyBytes([0x03])]);
        Assert.Equal("4", states[new KeyBytes([0x04])]);
        Assert.Equal(ImmutableSortedDictionary<string, string>.Empty, states[new KeyBytes([0xbe, 0xef])]);
    }

    [Fact]
    public void IterateNodes()
    {
        var stateStore = new TrieStateStore();
        var trie = Libplanet.Store.Trie.Trie.Create(
            ([0xbe, 0xef], ImmutableSortedDictionary<string, string>.Empty.Add("a", "b")));

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
        var stateStore = new TrieStateStore();
        string[] keys =
        [
            "1b418c98",
            "__3b8a",
            "___",
        ];
        var keyValues = keys
            .Select(key => ((KeyBytes)key, (object)key))
            .ToArray();
        var trie = Libplanet.Store.Trie.Trie.Create(keyValues);
        var prefixKey = (KeyBytes)"_";

        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.False(trie.TryGetNode(prefixKey, out _));
        Assert.False(trie.TryGetNode(prefixKey, out _));

        trie = trie.Set(extraKey, extraKey);
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(3, trie.GetNode(prefixKey).SelfAndDescendants().OfType<ValueNode>().Count());
        Assert.Equal(3, trie.GetNode(prefixKey).KeyValues().Count());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Set(bool commit)
    {
        var stateStore = new TrieStateStore();
        var trie = Libplanet.Store.Trie.Trie.Create(
            ((KeyBytes)"_", ImmutableSortedDictionary<string, string>.Empty));

        Assert.Throws<KeyNotFoundException>(() => trie[[0xbe, 0xef]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0x11, 0x22]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0xaa, 0xbb]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0x12, 0x34]]);

        trie = trie.Set([0xbe, 0xef], "null");
        trie = commit ? stateStore.Commit(trie) : trie;
        // Assert.Equal(
        //     Parse("3b6414adc582fcc2d44c0f85be521aad6a98b88d5b685006eb4b37ca314df23d"),
        //     trie.Hash);
        Assert.Equal("null", trie[[0xbe, 0xef]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0x11, 0x22]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0xaa, 0xbb]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0x12, 0x34]]);

        trie = trie.Set([0xbe, 0xef], true);
        trie = commit ? stateStore.Commit(trie) : trie;
        // Assert.Equal(
        //     Parse("5af9a61e8f0d48e4f76b920ae0a279008dfce6abb1c99fa8dfbd23b723949ed4"),
        //     trie.Hash);
        Assert.True(trie[[0xbe, 0xef]] is true);
        Assert.Throws<KeyNotFoundException>(() => trie[[0x11, 0x22]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0xaa, 0xbb]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0x12, 0x34]]);

        trie = trie.Set([0x11, 0x22], new List<string>());
        trie = commit ? stateStore.Commit(trie) : trie;
        // Assert.Equal(
        //     Parse("129d5b1ce5ff32577ac015678388984a0ffbd1beb5a38dac9880ceed9de50731"),
        //     trie.Hash);
        Assert.True(trie[[0xbe, 0xef]] is true);
        Assert.Equal<string>([], (List<string>)trie[[0x11, 0x22]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0xaa, 0xbb]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0x12, 0x34]]);

        trie = trie.Set([0xaa, 0xbb], "hello world");
        trie = commit ? stateStore.Commit(trie) : trie;
        // Assert.Equal(
        //     Parse("7f3e9047e58bfa31edcf4bf3053de808565f0673063fa80c3442b791635a33b3"),
        //     trie.Hash);
        Assert.True(trie[[0xbe, 0xef]] is true);
        Assert.Equal<string>([], (List<string>)trie[[0x11, 0x22]]);
        Assert.Equal("hello world", trie[[0xaa, 0xbb]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0x12, 0x34]]);

        // Once node encoding length exceeds certain length,
        // uncommitted and committed hash diverge
        var longText = string.Join("\n", Range(0, 1000).Select(i => $"long str {i}"));
        trie = trie.Set([0xaa, 0xbb], longText);
        trie = commit ? stateStore.Commit(trie) : trie;
        // Assert.Equal(
        //     Parse(commit
        //         ? "56e5a39a726acba1f7631a6520ae92e20bb93ca3992a7b7d3542c6daee68e56d"
        //         : "200481e87f2cc1c0729beb4526de7c54e065e0892e58667e0cbd530b85c4e728"),
        //     trie.Hash);
        Assert.True(trie[[0xbe, 0xef]] is true);
        Assert.Equal<string>([], (List<string>)trie[[0x11, 0x22]]);
        Assert.Equal(longText, trie[[0xaa, 0xbb]]);
        Assert.Throws<KeyNotFoundException>(() => trie[[0x12, 0x34]]);

        trie = trie.Set([0x12, 0x34], ImmutableSortedDictionary<string, string>.Empty);
        trie = commit ? stateStore.Commit(trie) : trie;
        // Assert.Equal(
        //     Parse(commit
        //         ? "88d6375097fd03e6c30a129eb0030d938caeaa796643971ca938fbd27ff5e057"
        //         : "18532d2ee8484a65b102668715c97decf1a3218b23bfb11933748018179cb5cf"),
        //     trie.Hash);
        Assert.True(trie[[0xbe, 0xef]] is true);
        Assert.Equal<string>([], (List<string>)trie[[0x11, 0x22]]);
        Assert.Equal(longText, trie[[0xaa, 0xbb]]);
        Assert.Equal(ImmutableSortedDictionary<string, string>.Empty, trie[[0x12, 0x34]]);

        var complexList = ImmutableList<object>.Empty
            .Add("Hello world")
            .Add(ImmutableSortedDictionary<string, object>.Empty
                .Add("foo", 1)
                .Add("bar", 2)
                .Add(
                    "lst",
                    new List<string>(Range(0, 1000).Select(i => $"long str {i}"))))
            .Add(new List<string>(Range(0, 1000).Select(i => $"long str {i}")));
        trie = trie.Set([0x11, 0x22], complexList);
        trie = commit ? stateStore.Commit(trie) : trie;
        // Assert.Equal(
        //     Parse(commit
        //         ? "f29820df65c1d1a66b69a59b9fe3e21911bbd2d97a9f298853c529804bf84a26"
        //         : "408037f213067c016c09466e75edcb80b2ad5de738be376ee80a364b4cab575a"),
        //     trie.Hash);
        Assert.True(trie[[0xbe, 0xef]] is true);
        Assert.True(ModelResolver.Equals(complexList, (ImmutableList<object>)trie[[0x11, 0x22]]));
        Assert.Equal(longText, trie[[0xaa, 0xbb]]);
        Assert.Equal(ImmutableSortedDictionary<string, string>.Empty, trie[[0x12, 0x34]]);

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
        trie = trie.Set([0x12, 0x34], complexDict);
        trie = commit ? stateStore.Commit(trie) : trie;
        // Assert.Equal(
        //     Parse(commit
        //         ? "1dabec2c0fea02af0182e9fee6c7ce7ad1a9d9bcfaa2cd80c2971bbce5272655"
        //         : "4783d18dfc8a2d4d98f722a935e45bd7fc1d0197fb4d33e62f734bfde968af39"),
        //     trie.Hash);
        Assert.True(trie[[0xbe, 0xef]] is true);
        Assert.Equal(complexList, trie[[0x11, 0x22]]);
        Assert.Equal(longText, trie[[0xaa, 0xbb]]);
        Assert.Equal(complexDict, trie[[0x12, 0x34]]);
    }

    [Fact]
    public void GetNode()
    {
        var stateStore = new TrieStateStore();
        var keyValues = new (ImmutableArray<byte>, object)[]
        {
            ([0x00], "00"),
            ([0x00, 0x00], "0000"),
            ([0x00, 0x10], "00000000000000000000000000000000_0010"),
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

    // [Fact]
    // public void ResolveToValueAtTheEndOfShortNode()
    // {
    //     var stateStore = new TrieStateStore();
    //     var trie = Libplanet.Store.Trie.Trie.Create(
    //         (Key: [0x00], Value: new Text("00")));

    //     trie = stateStore.Commit(trie);

    //     Assert.Throws<KeyNotFoundException>(() => trie[key: [0x00, 0x00]]);
    // }

    // [Fact]
    // public void SetValueToExtendedKey()
    // {
    //     var stateStore = new TrieStateStore();
    //     var value00 = new Text("00");
    //     var value0000 = new Text("0000");
    //     var trie = Libplanet.Store.Trie.Trie.Create(
    //         (Key: [0x00], Value: value00),
    //         (Key: [0x00, 0x00], Value: value0000));

    //     trie = stateStore.Commit(trie);

    //     Assert.Equal(2, trie.ToDictionary().Count);
    //     Assert.Equal(value00, trie[[0x00]]);
    //     Assert.Equal(value0000, trie[[0x00, 0x00]]);
    // }

    // [Fact]
    // public void SetValueToFullNode()
    // {
    //     var stateStore = new TrieStateStore();
    //     var value00 = new Text("00");
    //     var value0000 = new Text("0000");
    //     var value0010 = new Text("0010");
    //     var trie = Libplanet.Store.Trie.Trie.Create(
    //         (Key: [0x00], Value: value00),
    //         (Key: [0x00, 0x00], Value: value0000),
    //         (Key: [0x00, 0x10], Value: value0010));

    //     trie = stateStore.Commit(trie);

    //     Assert.Equal(3, trie.ToDictionary().Count);
    //     Assert.Equal(value00, trie[[0x00]]);
    //     Assert.Equal(value0000, trie[[0x00, 0x00]]);
    //     Assert.Equal(value0010, trie[[0x00, 0x10]]);
    // }

    // [Fact]
    // public void RemoveValue()
    // {
    //     var stateStore = new TrieStateStore();
    //     var key00 = new KeyBytes([0x00]);
    //     var value00 = new Text("00");
    //     var key0000 = new KeyBytes([0x00, 0x00]);
    //     var value0000 = new Text("0000");

    //     var trie = Libplanet.Store.Trie.Trie.Create(
    //         (Key: key00, Value: value00));
    //     trie = stateStore.Commit(trie);
    //     Assert.Null(trie.Remove(key00));

    //     trie = Libplanet.Store.Trie.Trie.Create(
    //         (Key: key0000, Value: value0000));
    //     trie = stateStore.Commit(trie);
    //     int expectedNodeCount = trie.IterateNodes().Count();
    //     int expectedValueCount = trie.ToDictionary().Count;
    //     HashDigest<SHA256> expectedHash = trie.Hash;

    //     trie = Libplanet.Store.Trie.Trie.Create(
    //         (Key: key00, Value: value00),
    //         (Key: key0000, Value: value0000));
    //     trie = stateStore.Commit(trie);
    //     trie = trie.Remove(key00);
    //     trie = stateStore.Commit(trie);
    //     Assert.Equal(value0000, trie[[0x00, 0x00]]);
    //     Assert.Equal(expectedNodeCount, trie.IterateNodes().Count());
    //     Assert.Equal(expectedValueCount, trie.ToDictionary().Count);
    //     Assert.Equal(expectedHash, trie.Hash);

    //     trie = Libplanet.Store.Trie.Trie.Create(
    //         (Key: key00, Value: value00));
    //     trie = stateStore.Commit(trie);
    //     expectedNodeCount = trie.IterateNodes().Count();
    //     expectedValueCount = trie.ToDictionary().Count;
    //     expectedHash = trie.Hash;

    //     trie = Libplanet.Store.Trie.Trie.Create(
    //         (Key: key00, Value: value00),
    //         (Key: key0000, Value: value0000));
    //     trie = stateStore.Commit(trie);
    //     trie = trie.Remove(key0000);
    //     trie = stateStore.Commit(trie);
    //     Assert.Equal(value00, Assert.Single(trie.ToDictionary()).Value);
    //     Assert.Equal(expectedNodeCount, trie.IterateNodes().Count());
    //     Assert.Equal(expectedValueCount, trie.ToDictionary().Count);
    //     Assert.Equal(expectedHash, trie.Hash);

    //     trie = Libplanet.Store.Trie.Trie.Create(
    //         (Key: key00, Value: value00),
    //         (Key: key0000, Value: value0000));
    //     trie = stateStore.Commit(trie);
    //     HashDigest<SHA256> hash = trie.Hash; // A reference to an earlier point in time.
    //     trie = trie.Remove(key00);
    //     Assert.Null(trie.Remove(key0000));

    //     trie = stateStore.GetStateRoot(hash);
    //     Assert.Equal(value00, trie[[0x00]]); // Nothing is actually removed from storage.
    //     Assert.Equal(value0000, trie[[0x00, 0x00]]);

    //     // Add randomized kvs and remove kvs in order.
    //     // The way the test is set up, identical kv pairs shouldn't matter.
    //     Random random = new Random();
    //     List<(KeyBytes Key, Text Value)> kvs = Enumerable
    //         .Range(0, 100)
    //         .Select(_ => TestUtils.GetRandomBytes(random.Next(2, 10)))
    //         .Select(bytes => (new KeyBytes(bytes), new Text(ByteUtility.Hex(bytes))))
    //         .ToList();
    //     var expected = new Stack<(HashDigest<SHA256>, int, int)>();

    //     for (var i = 0; i < kvs.Count; i++)
    //     {
    //         var kv = kvs[i];
    //         trie = i == 0 ? Libplanet.Store.Trie.Trie.Create(kv) : trie.Set(kv.Key, kv.Value);
    //         trie = stateStore.Commit(trie);
    //         expected.Push(
    //             (trie.Hash, trie.IterateNodes().Count(), trie.Count()));
    //     }

    //     for (var i = kvs.Count - 1; i >= 0; i--)
    //     {
    //         var (key, value) = kvs[i];
    //         var tuple = expected.Pop();
    //         Assert.Equal(tuple.Item3, trie.Count());
    //         Assert.Equal(tuple.Item2, trie.IterateNodes().Count());
    //         Assert.Equal(tuple.Item1, trie.Hash);
    //         trie = trie.Remove(key);
    //         trie = trie is not null ? stateStore.Commit(trie) : null;
    //     }

    //     Assert.Empty(expected);
    //     Assert.Null(trie);
    // }

    // [Fact]
    // public void RemoveValueNoOp()
    // {
    //     var stateStore = new TrieStateStore();
    //     var key00 = new KeyBytes([0x00]);
    //     var key0000 = new KeyBytes([0x00, 0x00]);
    //     var value0000 = new Text("0000");
    //     var key0011 = new KeyBytes([0x00, 0x11]);
    //     var value0011 = new Text("0011");
    //     var key000000 = new KeyBytes([0x00, 0x00, 0x00]);
    //     var trie = Libplanet.Store.Trie.Trie.Create(
    //         (Key: key0000, Value: value0000),
    //         (Key: key0011, Value: value0011));
    //     trie = stateStore.Commit(trie);
    //     int expectedNodeCount = trie.IterateNodes().Count();
    //     int expectedValueCount = trie.ToDictionary().Count;
    //     HashDigest<SHA256> expectedHash = trie.Hash;

    //     trie = trie.Remove(key00);
    //     trie = trie.Remove(key000000);
    //     trie = stateStore.Commit(trie);
    //     Assert.Equal(expectedNodeCount, trie.IterateNodes().Count());
    //     Assert.Equal(expectedValueCount, trie.Count());
    //     Assert.Equal(expectedHash, trie.Hash);
    // }
}
