// using System.IO;
// using Libplanet.Action;
// using Libplanet.Store;
// using Libplanet.Store.Trie;

// namespace Libplanet.Tests.Store;

// public class DefaultStoreFixture : StoreFixture
// {
//     public DefaultStoreFixture(
//         bool memory = true)
//         : base(CreateDefaultStore(memory), new DefaultKeyValueStore())
//     {
//         // if (memory)
//         // {
//         //     Path = string.Empty;
//         // }
//         // else
//         // {
//         //     Path = System.IO.Path.Combine(
//         //         System.IO.Path.GetTempPath(),
//         //         $"defaultstore_test_{Guid.NewGuid()}");
//         // }

//         // Scheme = "default+file://";

//         // var store = new DefaultStore(
//         //     new DefaultStoreOptions
//         //     {
//         //         Path = Path,
//         //         BlockCacheSize = 2,
//         //         TxCacheSize = 2,
//         //     });
//         // Store = store;
//     }

//     private static DefaultStore CreateDefaultStore(bool memory)
//     {
//         var path = string.Empty;
//         if (!memory)
//         {
//             path = System.IO.Path.Combine(
//                 System.IO.Path.GetTempPath(),
//                 $"defaultstore_test_{Guid.NewGuid()}");
//         }

//         // Scheme = "default+file://";

//         var store = new DefaultStore(
//             new DefaultStoreOptions
//             {
//                 Path = path,
//                 BlockCacheSize = 2,
//                 TxCacheSize = 2,
//             });
//         return store;
//     }

//     public TrieStateStore LoadTrieStateStore(string path)
//     {
//         IKeyValueStore stateKeyValueStore =
//             new DefaultKeyValueStore(path == string.Empty
//                 ? string.Empty
//                 : System.IO.Path.Combine(path, "states"));
//         return new TrieStateStore(stateKeyValueStore);
//     }

//     protected override void Dispose(bool disposing)
//     {
//         Store.Dispose();

//         // if (Directory.Exists(Path))
//         // {
//         //     Directory.Delete(Path, true);
//         // }
//     }
// }
