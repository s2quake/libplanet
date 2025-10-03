using System.Security.Cryptography;
using Libplanet.Serialization;
using Libplanet.State.Structures;
using Libplanet.State.Structures.Nodes;
using Libplanet.Types;
using static System.Linq.Enumerable;
using Libplanet.Data;

namespace Libplanet.State.Tests.Structures;

public sealed partial class TrieTest
{
    [Fact]
    public void ConstructWithHashDigest()
    {
        var stateIndex = new StateIndex();
        var hashDigest = Rand.HashDigest<SHA256>();
        var trie = new Trie(new HashNode { Hash = hashDigest, StateIndex = stateIndex });
        Assert.Equal(hashDigest, trie.Hash);
    }

    [Fact]
    public void ConstructWithRootNode()
    {
        var stateIndex = new StateIndex();
        var hashDigest = Rand.HashDigest<SHA256>();
        var node = new HashNode { Hash = hashDigest, StateIndex = stateIndex };
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
        var stateIndex = new StateIndex();
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

        trie = stateIndex.Commit(trie);
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
                ? "53f9d14147ba0893de6919fdb305ea63addda6e50ccf57c36d462fd729c18ecd"
                : "113ed14812ef98a3bde3f561fc1dd8a52918dcbc2c89b89a15ff21f92682feb0"),
            trie.Hash);
        Assert.Equal("null", trie["0xbe, 0xef"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x11, 0x22"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0xbe, 0xef", true);
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "d8932360e9ee3f88bb66619469e4cd7a83d01ec4e2c3a0620eff41a65c552dd1"
                : "9495b5aac1b05f7554c0eb8ca49d13cd9d529d6cd3d6ee7eee3c7c0139b0e027"),
            trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Throws<KeyNotFoundException>(() => trie["0x11, 0x22"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0x11, 0x22", new List<string>());
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "4265ded0fac96433263881c61ebd0a3801d2a9ba700c899b18a97ed9d0b0f2de"
                : "b793b539a1912e2c428454e1e0eac66e72c566e2cd2b3623c525ae13fedc8d07"),
            trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Equal<string>([], (List<string>)trie["0x11, 0x22"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0xaa, 0xbb", "hello world");
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "6c690e496876ad1114b52222d7d3227c0fecee076aae64c5cf7042e20f62fb87"
                : "26e88dbe5212de11f0ac640872555b05c6a0faa6ec036a42316f18e7465f6262"),
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
                ? "5f1d887f543a4434a372236694868e3baef42c522bab169f299b09713e157d16"
                : "4d578f246fc585f7473e0145228a249bb3828dc3853e372b300897b4f84c1aaa"),
            trie.Hash);
        Assert.True(trie["0xbe, 0xef"] is true);
        Assert.Equal<string>([], (List<string>)trie["0x11, 0x22"]);
        Assert.Equal(longText, trie["0xaa, 0xbb"]);
        Assert.Throws<KeyNotFoundException>(() => trie["0x12, 0x34"]);

        trie = trie.Set("0x12, 0x34", ImmutableSortedDictionary<string, string>.Empty);
        trie = commit ? stateStore.Commit(trie) : trie;
        Assert.Equal(
            HashDigest<SHA256>.Parse(commit
                ? "8827dd4949f63334b0932a194663ba1ab92de4ca1889a964a835528ba0e485a8"
                : "960135ab5509e291d246ee3dabb9bd290cc92e133fefd1b193a3f68f6640242d"),
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
                ? "d04a1cea17b9b79000900093ce934dab2eb959a55ae94b0efef932f1bd991c39"
                : "8f73bc639a04328e8b9d7097f4f25b27ed5e88040a78668ba826c51583d8645e"),
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
                ? "efb648ea8ef85b63aea3da0f4209952ece491d74501e023ac5151ccff329480c"
                : "a48dbf7d28bc2ea810cafb78cb478e8df3f36a5964b14a4cf69f83bfae95e632"),
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
        var kvs = Rand.HashSet(Rand.Word, 100)
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
