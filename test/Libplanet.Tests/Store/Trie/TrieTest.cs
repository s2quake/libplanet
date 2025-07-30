// using Bencodex.Types;
// using Libplanet.Store;
// using Libplanet.Store.Trie;
// using Libplanet.Types.Crypto;

// namespace Libplanet.Tests.Store.Trie;

// public class TrieTest
// {
//     [Theory]
//     [InlineData(2)]
//     [InlineData(4)]
//     [InlineData(8)]
//     [InlineData(16)]
//     [InlineData(128)]
//     [InlineData(1024)]
//     public void GetAndSet(int addressCount)
//     {
//         var keyValueStore = new MemoryTable();
//         ITrie trie = Libplanet.Store.Trie.Trie.Create(hashDigest: default, keyValueStore);

//         var addresses = Enumerable
//             .Range(0, addressCount)
//             .Select(_ => new PrivateKey().Address)
//             .ToImmutableArray();
//         var states = new Dictionary<Address, IValue>();

//         void CheckAddressStates()
//         {
//             foreach (var address in addresses)
//             {
//                 IValue v = trie.GetMany(new[] { new KeyBytes(address.Bytes) })[0];
//                 IValue expectedState = states.ContainsKey(address) ? states[address] : null;
//                 Assert.Equal(expectedState, v);
//             }
//         }

//         foreach (var address in addresses)
//         {
//             states[address] = (Text)address.ToString("raw", null);
//             trie = trie.Set(new KeyBytes(address.Bytes), states[address]);
//             CheckAddressStates();
//         }
//     }

//     [Theory]
//     [InlineData(2)]
//     [InlineData(4)]
//     [InlineData(8)]
//     [InlineData(16)]
//     [InlineData(128)]
//     [InlineData(1024)]
//     public void Commit(int addressCount)
//     {
//         var keyValueStore = new MemoryTable();
//         TrieStateStore stateStore = new TrieStateStore(keyValueStore);
//         ITrie trieA = stateStore.GetStateRoot(default);

//         var addresses = new Address[addressCount];
//         var states = new IValue[addressCount];
//         for (int i = 0; i < addressCount; ++i)
//         {
//             addresses[i] = new PrivateKey().Address;
//             states[i] = (Binary)TestUtils.GetRandomBytes(128);

//             trieA = trieA.Set(new KeyBytes(addresses[i].Bytes), states[i]);
//         }

//         KeyBytes path = new KeyBytes(TestUtils.GetRandomBytes(32));
//         trieA = trieA.Set(path, (Text)"foo");
//         Assert.Equal((Text)"foo", trieA.GetMany(new[] { path })[0]);

//         ITrie trieB = stateStore.Commit(trieA);
//         Assert.Equal((Text)"foo", trieB.GetMany(new[] { path })[0]);

//         trieB = trieB.Set(path, (Text)"bar");
//         Assert.Equal((Text)"foo", trieA.GetMany(new[] { path })[0]);
//         Assert.Equal((Text)"bar", trieB.GetMany(new[] { path })[0]);

//         ITrie trieC = stateStore.Commit(trieB);
//         ITrie trieD = stateStore.Commit(trieC);

//         Assert.NotEqual(trieA.Hash, trieB.Hash);
//         Assert.NotEqual(trieA.Hash, trieC.Hash);
//         Assert.NotEqual(trieB.Hash, trieC.Hash);
//         Assert.Equal(trieC.Hash, trieD.Hash);
//     }

//     [Fact]
//     public void EmptyRootHash()
//     {
//         var keyValueStore = new MemoryTable();
//         TrieStateStore stateStore = new TrieStateStore(keyValueStore);
//         ITrie trie = stateStore.GetStateRoot(default);
//         Assert.Equal(default, trie.Hash);

//         var committedTrie = stateStore.Commit(trie);
//         Assert.Equal(default, committedTrie.Hash);

//         trie = trie.Set(new KeyBytes(default(Address).Bytes), Dictionary.Empty);
//         committedTrie = stateStore.Commit(trie);
//         Assert.NotEqual(default, committedTrie.Hash);
//     }
// }
