// // using Libplanet.Data.Structures;
// using Libplanet.Data.Structures.Nodes;

// namespace Libplanet.Tests.Store.Trie.Nodes;

// public class ShortNodeTest
// {
//     [Fact]
//     public void ToBencodex()
//     {
//         var shortNode = new ShortNode
//         {
//             Key = Nibbles.Parse("beef"),
//             Value = new ValueNode { Value = "foo" },
//         };

//         var expected =
//             new List(
//             [
//                 (Binary)Nibbles.Parse("beef").ByteArray,
//                 new List([null, "foo"]),
//             ]);
//         var encoded = shortNode.ToBencodex();
//         Assert.IsType<List>(encoded);
//         Assert.Equal(expected.Count, ((List)encoded).Count);
//         Assert.Equal(expected, encoded);
//     }
// }
