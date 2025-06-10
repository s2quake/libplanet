using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.Data.Structures;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Types;
using Libplanet.Types.Tests;
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
                ? "0f629bfaa6a91eef0ff4abc4656282dc03dc2ec04b3657c5c089f68bd0dd347b"
                : "a390cb3d31ce0f310204072d36abee200fc4fa8cadc00a6a7850c1d7ca93a47e"),
            trie.Hash);
        Assert.Equal("null", trie["0xbe, 0xef"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x11, 0x22"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0xbe, 0xef", true);
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "d843063e618a85d6f3bdc44eb50fd90f08e4bfec388cc4e36eca5a710b9a1178"
                : "32bc454aea4f116eaf55eeea7fc997cef8b3e617640e7b08e55f12ef5eec21c3"),
            trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Throws<KeyNotFoundException>(() => trie["0x11, 0x22"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0x11, 0x22", new List<string>());
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "a7861e19487071409d67460633d7b90d5fe1f561220e92b3c4a9bdefb76ecc8e"
                : "c6eb8995a83d9f9c14b01dd2671bdb6a930c1f5c3ee54b0bcec523bbd764bb4f"),
            trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Equal<string>([], (List<string>)trie["0x11, 0x22"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0xaa, 0xbb", "hello world");
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "5669b367c51a123727c4617e10ccf98681ff05f6435f6ec878c67c1f63c09744"
                : "b8b7c0a190abdb04c6f2715197ff27d04c88079af94149ad5339aab33c0c3ae8"),
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
                ? "5c914a620e58eb66d9bc9611a09bca1e37962f8ba6ee05c80c17ff67e15a74a1"
                : "d869a6da787ea7625e0bcb377c40ab6169630251cdd8cde1efaaa25cb75c2be5"),
            trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Equal<string>([], (List<string>)trie["0x11, 0x22"]);
        Assert.Equal(longText, trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0x12, 0x34", ImmutableSortedDictionary<string, string>.Empty);
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "a613f0344a6384d420767f66ce49ac8f74a0283f4f0a1d69276b92b0ef9f4c76"
                : "e8876c98e212b063ccf5111785bf7d96c8d00755a2b08be7450c656628689495"),
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
                ? "892d300adc43befc6b57348bdbe4674ab0272f3604deaada851865e8e67516f6"
                : "960b217880c33e07c74af077e0b26bd0e636dc34a9b04bc139925601ab97691a"),
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
                ? "ea933057a8d953e4e88191a495442dc45359862cc2d8871ab3ead88942f599a9"
                : "f09781287f2160aba0153473d3ccea45c4df929c1af4e155480c498a75199af8"),
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
