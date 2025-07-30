using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Serialization;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Xunit;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Store;

public class TrieStateStoreTest
{
    private readonly IKeyValueStore _stateKeyValueStore;

    public TrieStateStoreTest()
    {
        _stateKeyValueStore = new DefaultKeyValueStore();
    }

    public static KeyBytes KeyFoo { get; } = (KeyBytes)"foo";

    public static KeyBytes KeyBar { get; } = (KeyBytes)"bar";

    public static KeyBytes KeyBaz { get; } = (KeyBytes)"baz";

    public static KeyBytes KeyQux { get; } = (KeyBytes)"qux";

    public static KeyBytes KeyQuux { get; } = (KeyBytes)"quux";

    [Fact]
    public void GetStateRoot()
    {
        var stateStore = new TrieStateStore(_stateKeyValueStore);
        var emptyTrie = stateStore.GetStateRoot(default);
        Assert.True(emptyTrie.IsCommitted);
        Assert.False(emptyTrie.ContainsKey(KeyFoo));
        Assert.False(emptyTrie.ContainsKey(KeyBar));
        Assert.False(emptyTrie.ContainsKey(KeyBaz));
        Assert.False(emptyTrie.ContainsKey(KeyQux));
        Assert.False(emptyTrie.ContainsKey(KeyQuux));

        KeyBytes fooKey = (KeyBytes)"foo";
        KeyBytes barKey = (KeyBytes)"bar";
        KeyBytes bazKey = (KeyBytes)"baz";
        KeyBytes quxKey = (KeyBytes)"qux";
        var values = ImmutableDictionary<KeyBytes, IValue>.Empty
            .Add(fooKey, (Binary)GetRandomBytes(32))
            .Add(barKey, (Text)ByteUtil.Hex(GetRandomBytes(32)))
            .Add(bazKey, (Bencodex.Types.Boolean)false)
            .Add(quxKey, Bencodex.Types.Dictionary.Empty);
        ITrie trie = stateStore.Commit(
            values.Aggregate(
                stateStore.GetStateRoot(default),
                (prev, kv) => prev.Set(kv.Key, kv.Value)));
        HashDigest<SHA256> hash = trie.Hash;
        ITrie found = stateStore.GetStateRoot(hash);
        Assert.True(found.IsCommitted);
        AssertBencodexEqual(values[fooKey], found.GetMany(new[] { KeyFoo })[0]);
        AssertBencodexEqual(values[barKey], found.GetMany(new[] { KeyBar })[0]);
        AssertBencodexEqual(values[bazKey], found.GetMany(new[] { KeyBaz })[0]);
        AssertBencodexEqual(values[quxKey], found.GetMany(new[] { KeyQux })[0]);
        Assert.Null(found.GetMany(new[] { KeyQuux })[0]);
    }

    [Fact]
    public void CopyStates()
    {
        var stateStore = new TrieStateStore(_stateKeyValueStore);
        IKeyValueStore targetStateKeyValueStore = new MemoryKeyValueStore();
        var targetStateStore = new TrieStateStore(targetStateKeyValueStore);
        Random random = new();
        List<(KeyBytes, IValue)> kvs = Enumerable.Range(0, 1_000)
            .Select(_ =>
            (
                KeyBytes.Create(GetRandomBytes(random.Next(1, 20))),
                (IValue)new Binary(GetRandomBytes(20))
            ))
            .ToList();

        ITrie trie = stateStore.GetStateRoot(default);
        foreach (var kv in kvs)
        {
            trie = trie.Set(kv.Item1, kv.Item2);
        }

        trie = stateStore.Commit(trie);
        int prevStatesCount = _stateKeyValueStore.Keys.Count();

        // NOTE: Avoid possible collision of KeyBytes, just in case.
        _stateKeyValueStore[KeyBytes.Create(GetRandomBytes(30))] = ByteUtil.ParseHex("00");
        _stateKeyValueStore[KeyBytes.Create(GetRandomBytes(40))] = ByteUtil.ParseHex("00");

        Assert.Equal(prevStatesCount + 2, _stateKeyValueStore.Keys.Count());
        Assert.Empty(targetStateKeyValueStore.Keys);

        stateStore.CopyStates(
            ImmutableHashSet<HashDigest<SHA256>>.Empty.Add(trie.Hash),
            targetStateStore);

        // It will stay at the same count of nodes.
        // FIXME: Bencodex fingerprints also should be tracked.
        //        https://github.com/planetarium/libplanet/issues/1653
        Assert.Equal(prevStatesCount, targetStateKeyValueStore.Keys.Count());
        Assert.Equal(
            trie.IterateNodes().Count(),
            targetStateStore.GetStateRoot(trie.Hash).IterateNodes().Count());
        Assert.Equal(
            trie.ToDictionary().Count,
            targetStateStore.GetStateRoot(trie.Hash).ToDictionary().Count);
    }

    [Fact]
    public void CopyWorldStates()
    {
        var stateStore = new TrieStateStore(_stateKeyValueStore);
        IKeyValueStore targetStateKeyValueStore = new MemoryKeyValueStore();
        var targetStateStore = new TrieStateStore(targetStateKeyValueStore);
        Random random = new();
        Dictionary<Address, List<(KeyBytes, IValue)>> data = Enumerable
            .Range(0, 20)
            .Select(_ => new Address([.. GetRandomBytes(Address.Size)]))
            .ToDictionary(
                address => address,
                _ => Enumerable
                    .Range(0, 100)
                    .Select(__ =>
                    (
                        KeyBytes.Create(GetRandomBytes(random.Next(20))),
                        (IValue)new Binary(GetRandomBytes(20))
                    ))
                    .ToList());

        ITrie worldTrie = stateStore.GetStateRoot(default);
        worldTrie = worldTrie.SetMetadata(new TrieMetadata(5));

        List<HashDigest<SHA256>> accountHashes = new();
        foreach (var elem in data)
        {
            ITrie trie = stateStore.GetStateRoot(default);
            foreach (var kv in elem.Value)
            {
                trie = trie.Set(kv.Item1, kv.Item2);
            }

            trie = stateStore.Commit(trie);
            worldTrie = worldTrie.Set(
                new KeyBytes(elem.Key.ByteArray), 
                ModelSerializer.Serialize(trie.Hash));
            accountHashes.Add(trie.Hash);
        }

        worldTrie = stateStore.Commit(worldTrie);
        int prevStatesCount = _stateKeyValueStore.Keys.Count();

        // NOTE: Avoid possible collision of KeyBytes, just in case.
        _stateKeyValueStore[KeyBytes.Create(GetRandomBytes(30))] = ByteUtil.ParseHex("00");
        _stateKeyValueStore[KeyBytes.Create(GetRandomBytes(40))] = ByteUtil.ParseHex("00");

        Assert.Equal(prevStatesCount + 2, _stateKeyValueStore.Keys.Count());
        Assert.Empty(targetStateKeyValueStore.Keys);

        stateStore.CopyStates(
            ImmutableHashSet<HashDigest<SHA256>>.Empty.Add(worldTrie.Hash),
            targetStateStore);

        // It will stay at the same count of nodes.
        // FIXME: Bencodex fingerprints also should be tracked.
        //        https://github.com/planetarium/libplanet/issues/1653
        Assert.Equal(prevStatesCount, targetStateKeyValueStore.Keys.Count());
        Assert.Equal(
            worldTrie.IterateNodes().Count(),
            targetStateStore.GetStateRoot(worldTrie.Hash).IterateNodes().Count());
        Assert.Equal(
            worldTrie.ToDictionary().Count,
            targetStateStore.GetStateRoot(worldTrie.Hash).ToDictionary().Count);
        Assert.Equal(
            stateStore.GetStateRoot(accountHashes.First()).IterateNodes().Count(),
            targetStateStore.GetStateRoot(accountHashes.First()).IterateNodes().Count());
        Assert.Equal(
            stateStore.GetStateRoot(accountHashes.First()).ToDictionary().Count,
            targetStateStore.GetStateRoot(accountHashes.First()).ToDictionary().Count);
    }

    [Fact]
#pragma warning disable S2699 // Tests should include assertions
    public void IdempotentDispose()
#pragma warning restore S2699 // Tests should include assertions
    {
        var stateStore = new TrieStateStore(_stateKeyValueStore);
        stateStore.Dispose();
#pragma warning disable S3966 // Objects should not be disposed more than once
        stateStore.Dispose();
#pragma warning restore S3966 // Objects should not be disposed more than once
    }
}
