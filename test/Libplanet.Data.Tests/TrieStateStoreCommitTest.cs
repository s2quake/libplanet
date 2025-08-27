using System.Security.Cryptography;
using Libplanet.Data;
using Libplanet.Data.Structures;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Types;

namespace Libplanet.Tests.Store;

public class TrieStateStoreCommitTest
{
    [Fact]
    public void CommitEmptyDoesNotWrite()
    {
        var keyValueStore = new MemoryTable();
        StateIndex stateStore = new StateIndex();
        Trie emptyTrie = stateStore.GetTrie(default);
        HashDigest<SHA256> emptyRootHash = emptyTrie.Hash;

        Assert.Null(emptyTrie.Node);
        Assert.True(stateStore.GetTrie(emptyRootHash).IsCommitted);
        Assert.Null(stateStore.GetTrie(emptyRootHash).Node);
        Assert.False(keyValueStore.ContainsKey(emptyRootHash.ToString()));

        emptyTrie = stateStore.Commit(emptyTrie);
        Assert.Null(emptyTrie.Node);
        Assert.Equal(emptyRootHash, emptyTrie.Hash);
        Assert.True(stateStore.GetTrie(emptyRootHash).IsCommitted);
        Assert.False(keyValueStore.ContainsKey(emptyRootHash.ToString()));
    }

    [Fact]
    public void Commit()
    {
        var keyValueStore = new MemoryTable();
        StateIndex stateStore = new StateIndex(keyValueStore);
        Trie trie = stateStore.GetTrie(default);

        trie = trie.Set("2c73", "2c73");
        trie = trie.Set("234f", "234f");

        HashDigest<SHA256> hashBeforeCommit = trie.Hash;
        trie = stateStore.Commit(trie);
        HashDigest<SHA256> hashAfterCommitOnce = trie.Hash;
        trie = stateStore.Commit(trie);
        HashDigest<SHA256> hashAfterCommitTwice = trie.Hash;

        Assert.NotEqual(hashBeforeCommit, hashAfterCommitOnce);
        Assert.Equal(hashAfterCommitOnce, hashAfterCommitTwice);
        Assert.False(stateStore.GetTrie(hashBeforeCommit).IsCommitted);
        Assert.True(stateStore.GetTrie(hashAfterCommitOnce).IsCommitted);
        Assert.False(keyValueStore.ContainsKey(hashBeforeCommit.ToString()));
        Assert.True(keyValueStore.ContainsKey(hashAfterCommitOnce.ToString()));

        trie = stateStore.GetTrie(hashAfterCommitOnce);
        Assert.Equal(2, trie.ToDictionary().Count);
        Assert.Equal("2c73", trie["2c73"]);
        Assert.Equal("234f", trie["234f"]);
    }

    [Fact]
    public void CommittedNonEmptyTrieRootIsHashNode()
    {
        var keyValueStore = new MemoryTable();
        StateIndex stateStore = new StateIndex(keyValueStore);
        Trie trie = stateStore.GetTrie(default);
        trie = trie.Set(string.Empty, 1);
        trie = stateStore.Commit(trie);
        HashNode root = Assert.IsType<HashNode>(trie.Node);
        trie = stateStore.GetTrie(trie.Hash);
        Assert.IsType<HashNode>(trie.Node);
        Assert.Equal(root, trie.Node);
    }
}
