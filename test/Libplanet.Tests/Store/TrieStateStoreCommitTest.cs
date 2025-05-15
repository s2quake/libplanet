using System.Security.Cryptography;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Store.Trie.Nodes;
using Libplanet.Types;

namespace Libplanet.Tests.Store
{
    public class TrieStateStoreCommitTest
    {
        [Fact]
        public void CommitEmptyDoesNotWrite()
        {
            var keyValueStore = new MemoryTable();
            TrieStateStore stateStore = new TrieStateStore();
            ITrie emptyTrie = stateStore.GetStateRoot(default);
            HashDigest<SHA256> emptyRootHash = emptyTrie.Hash;

            Assert.Null(emptyTrie.Node);
            Assert.True(stateStore.GetStateRoot(emptyRootHash).IsCommitted);
            Assert.Null(stateStore.GetStateRoot(emptyRootHash).Node);
            Assert.False(keyValueStore.ContainsKey(new KeyBytes(emptyRootHash.Bytes)));

            emptyTrie = stateStore.Commit(emptyTrie);
            Assert.Null(emptyTrie.Node);
            Assert.Equal(emptyRootHash, emptyTrie.Hash);
            Assert.True(stateStore.GetStateRoot(emptyRootHash).IsCommitted);
            Assert.False(keyValueStore.ContainsKey(new KeyBytes(emptyRootHash.Bytes)));
        }

        [Fact]
        public void Commit()
        {
            var keyValueStore = new MemoryTable();
            TrieStateStore stateStore = new TrieStateStore(keyValueStore);
            ITrie trie = stateStore.GetStateRoot(default);

            trie = trie.Set(new KeyBytes(new byte[] { 0x2c, 0x73 }), "2c73");
            trie = trie.Set(new KeyBytes(new byte[] { 0x23, 0x4f }), "234f");

            HashDigest<SHA256> hashBeforeCommit = trie.Hash;
            trie = stateStore.Commit(trie);
            HashDigest<SHA256> hashAfterCommitOnce = trie.Hash;
            trie = stateStore.Commit(trie);
            HashDigest<SHA256> hashAfterCommitTwice = trie.Hash;

            Assert.NotEqual(hashBeforeCommit, hashAfterCommitOnce);
            Assert.Equal(hashAfterCommitOnce, hashAfterCommitTwice);
            Assert.False(stateStore.GetStateRoot(hashBeforeCommit).IsCommitted);
            Assert.True(stateStore.GetStateRoot(hashAfterCommitOnce).IsCommitted);
            Assert.False(keyValueStore.ContainsKey(new KeyBytes(hashBeforeCommit.Bytes)));
            Assert.True(keyValueStore.ContainsKey(new KeyBytes(hashAfterCommitOnce.Bytes)));

            trie = stateStore.GetStateRoot(hashAfterCommitOnce);
            Assert.Equal(2, trie.ToDictionary().Count);
            Assert.Equal("2c73", trie[new KeyBytes(new byte[] { 0x2c, 0x73 })]);
            Assert.Equal("234f", trie[new KeyBytes(new byte[] { 0x23, 0x4f })]);
        }

        [Fact]
        public void CommittedNonEmptyTrieRootIsHashNode()
        {
            var keyValueStore = new MemoryTable();
            TrieStateStore stateStore = new TrieStateStore(keyValueStore);
            ITrie trie = stateStore.GetStateRoot(default);
            trie = trie.Set(new KeyBytes([]), 1);
            trie = stateStore.Commit(trie);
            HashNode root = Assert.IsType<HashNode>(trie.Node);
            trie = stateStore.GetStateRoot(trie.Hash);
            Assert.IsType<HashNode>(trie.Node);
            Assert.Equal(root, trie.Node);
        }
    }
}
