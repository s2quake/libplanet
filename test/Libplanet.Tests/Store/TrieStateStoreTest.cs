using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.Data.Structures;
using Libplanet.Types;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Store;

public class TrieStateStoreTest
{
    private readonly ITable _stateKeyValueStore;

    public TrieStateStoreTest()
    {
        _stateKeyValueStore = new MemoryTable();
    }

    public static string KeyFoo { get; } = "foo";

    public static string KeyBar { get; } = "bar";

    public static string KeyBaz { get; } = "baz";

    public static string KeyQux { get; } = "qux";

    public static string KeyQuux { get; } = "quux";

    [Fact]
    public void GetStateRoot()
    {
        var stateStore = new StateIndex(_stateKeyValueStore);
        var emptyTrie = stateStore.GetTrie(default);
        Assert.True(emptyTrie.IsCommitted);
        Assert.False(emptyTrie.ContainsKey(KeyFoo));
        Assert.False(emptyTrie.ContainsKey(KeyBar));
        Assert.False(emptyTrie.ContainsKey(KeyBaz));
        Assert.False(emptyTrie.ContainsKey(KeyQux));
        Assert.False(emptyTrie.ContainsKey(KeyQuux));

        string fooKey = "foo";
        string barKey = "bar";
        string bazKey = "baz";
        string quxKey = "qux";
        var values = ImmutableDictionary<string, object>.Empty
            .Add(fooKey, GetRandomBytes(32))
            .Add(barKey, ByteUtility.Hex(GetRandomBytes(32)))
            .Add(bazKey, false)
            .Add(quxKey, null);
        Trie trie = stateStore.Commit(
            values.Aggregate(
                stateStore.GetTrie(default),
                (prev, kv) => prev.Set(kv.Key, kv.Value)));
        HashDigest<SHA256> hash = trie.Hash;
        Trie found = stateStore.GetTrie(hash);
        Assert.True(found.IsCommitted);
        Assert.Equal(values[fooKey], found[KeyFoo]);
        Assert.Equal(values[barKey], found[KeyBar]);
        Assert.Equal(values[bazKey], found[KeyBaz]);
        Assert.Equal(values[quxKey], found[KeyQux]);
        Assert.Null(found[KeyQuux]);
    }

    // [Fact]
    // public void CopyStates()
    // {
    //     var stateStore = new StateStore(_stateKeyValueStore);
    //     var targetStateKeyValueStore = new MemoryTable();
    //     var targetStateStore = new StateStore(targetStateKeyValueStore);
    //     Random random = new();
    //     List<(string, byte[])> kvs = Enumerable.Range(0, 1_000)
    //         .Select(_ =>
    //         (
    //             RandomUtility.Word(),
    //             GetRandomBytes(20)
    //         ))
    //         .ToList();

    //     ITrie trie = stateStore.GetStateRoot(default);
    //     foreach (var kv in kvs)
    //     {
    //         trie = trie.Set(kv.Item1, kv.Item2);
    //     }

    //     trie = stateStore.Commit(trie);
    //     int prevStatesCount = _stateKeyValueStore.Keys.Count();

    //     // NOTE: Avoid possible collision of string, just in case.
    //     _stateKeyValueStore[RandomUtility.Word()] = ByteUtility.ParseHex("00");
    //     _stateKeyValueStore[RandomUtility.Word()] = ByteUtility.ParseHex("00");

    //     Assert.Equal(prevStatesCount + 2, _stateKeyValueStore.Keys.Count());
    //     Assert.Empty(targetStateKeyValueStore.Keys);

    //     stateStore.CopyStates(
    //         ImmutableHashSet<HashDigest<SHA256>>.Empty.Add(trie.Hash),
    //         targetStateStore);

    //     // It will stay at the same count of nodes.
    //     // FIXME: Bencodex fingerprints also should be tracked.
    //     //        https://github.com/planetarium/libplanet/issues/1653
    //     Assert.Equal(prevStatesCount, targetStateKeyValueStore.Keys.Count());
    //     Assert.Equal(
    //         trie.Node.Traverse().Count(),
    //         targetStateStore.GetStateRoot(trie.Hash).Node.Traverse().Count());
    //     Assert.Equal(
    //         trie.ToDictionary().Count,
    //         targetStateStore.GetStateRoot(trie.Hash).ToDictionary().Count);
    // }

    // [Fact]
    // public void CopyWorldStates()
    // {
    //     var stateStore = new StateStore(_stateKeyValueStore);
    //     var targetStateKeyValueStore = new MemoryTable();
    //     var targetStateStore = new StateStore(targetStateKeyValueStore);
    //     Random random = new();
    //     Dictionary<Address, List<(string, byte[])>> data = Enumerable
    //         .Range(0, 20)
    //         .Select(_ => new Address([.. GetRandomBytes(Address.Size)]))
    //         .ToDictionary(
    //             address => address,
    //             _ => Enumerable
    //                 .Range(0, 100)
    //                 .Select(__ =>
    //                 (
    //                     RandomUtility.Word(),
    //                     GetRandomBytes(20)))
    //                 .ToList());

    //     ITrie worldTrie = stateStore.GetStateRoot(default);

    //     List<HashDigest<SHA256>> accountHashes = new();
    //     foreach (var elem in data)
    //     {
    //         ITrie trie = stateStore.GetStateRoot(default);
    //         foreach (var kv in elem.Value)
    //         {
    //             trie = trie.Set(kv.Item1, kv.Item2);
    //         }

    //         trie = stateStore.Commit(trie);
    //         worldTrie = worldTrie.Set(
    //             elem.Key.ToString(),
    //             ModelSerializer.SerializeToBytes(trie.Hash));
    //         accountHashes.Add(trie.Hash);
    //     }

    //     worldTrie = stateStore.Commit(worldTrie);
    //     int prevStatesCount = _stateKeyValueStore.Keys.Count();

    //     // NOTE: Avoid possible collision of string, just in case.
    //     _stateKeyValueStore[RandomUtility.Word()] = ByteUtility.ParseHex("00");
    //     _stateKeyValueStore[RandomUtility.Word()] = ByteUtility.ParseHex("00");

    //     Assert.Equal(prevStatesCount + 2, _stateKeyValueStore.Keys.Count());
    //     Assert.Empty(targetStateKeyValueStore.Keys);

    //     stateStore.CopyStates(
    //         ImmutableHashSet<HashDigest<SHA256>>.Empty.Add(worldTrie.Hash),
    //         targetStateStore);

    //     // It will stay at the same count of nodes.
    //     // FIXME: Bencodex fingerprints also should be tracked.
    //     //        https://github.com/planetarium/libplanet/issues/1653
    //     Assert.Equal(prevStatesCount, targetStateKeyValueStore.Keys.Count());
    //     Assert.Equal(
    //         worldTrie.Node.Traverse().Count(),
    //         targetStateStore.GetStateRoot(worldTrie.Hash).Node.Traverse().Count());
    //     Assert.Equal(
    //         worldTrie.ToDictionary().Count,
    //         targetStateStore.GetStateRoot(worldTrie.Hash).ToDictionary().Count);
    //     Assert.Equal(
    //         stateStore.GetStateRoot(accountHashes.First()).Node.Traverse().Count(),
    //         targetStateStore.GetStateRoot(accountHashes.First()).Node.Traverse().Count());
    //     Assert.Equal(
    //         stateStore.GetStateRoot(accountHashes.First()).ToDictionary().Count,
    //         targetStateStore.GetStateRoot(accountHashes.First()).ToDictionary().Count);
    // }

    [Fact]
#pragma warning disable S2699 // Tests should include assertions
    public void IdempotentDispose()
#pragma warning restore S2699 // Tests should include assertions
    {
        _ = new StateIndex(_stateKeyValueStore);
    }
}
