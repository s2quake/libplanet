// using System.Security.Cryptography;
// using Bencodex.Types;
// using Libplanet.Store.Trie.Nodes;
// using Libplanet.Types;

// namespace Libplanet.Tests.Store.Trie.Nodes
// {
//     public class HashNodeTest
//     {
//         [Fact]
//         public void ToBencodex()
//         {
//             var buf = new byte[128];
//             var random = new Random();
//             random.NextBytes(buf);
//             var hashDigest = HashDigest<SHA256>.Create(buf);

//             var valueNode = new HashNode { Hash = hashDigest };
//             Assert.Equal((Binary)hashDigest.Bytes.ToArray(), valueNode.ToBencodex());
//         }
//     }
// }
