// // using Libplanet.Data.Structures.Nodes;
// using Libplanet.Types;

// namespace Libplanet.Tests.Store.Trie.Nodes
// {
//     public class ValueNodeTest
//     {
//         [Fact]
//         public void ToBencodex()
//         {
//             var values = new IValue[]
//             {
//                 null,
//                 (Binary)ByteUtility.ParseHexToImmutable("beef"),
//                 (Integer)0xbeef,
//                 Dictionary.Empty,
//                 List.Empty,
//             };

//             foreach (var value in values)
//             {
//                 var valueNode = new ValueNode { Value = value };
//                 var expected = new List(new[]
//                 {
//                     null, value,
//                 });
//                 Assert.Equal(expected, valueNode.ToBencodex());
//             }
//         }
//     }
// }
