// // using Libplanet.Store.DataStructures;
// using Libplanet.Store.DataStructures.Nodes;

// namespace Libplanet.Tests.Store.Trie.Nodes;

// public class FullNodeTest
// {
//     [Fact]
//     public void ToBencodex()
//     {
//         var fullNode = new FullNode
//         {
//             Children = ImmutableDictionary<byte, INode>.Empty,
//             Value = new ValueNode { Value = Dictionary.Empty },
//         };

//         var expected =
//             new List(Enumerable.Repeat<IValue>(null, 16).ToImmutableArray()
//                 .Add(new List(null, Dictionary.Empty)));
//         var encoded = fullNode.ToBencodex();
//         Assert.IsType<List>(encoded);
//         Assert.Equal(expected.Count, ((List)encoded).Count);
//         Assert.Equal(expected, encoded);
//     }
// }
