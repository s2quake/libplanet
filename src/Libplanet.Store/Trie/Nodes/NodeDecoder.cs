// #pragma warning disable S1066 // Mergeable "if" statements should be combined
// using System.Security.Cryptography;
// // using Libplanet.Types;

// namespace Libplanet.Store.Trie.Nodes;

// public static class NodeDecoder
// {
//     public const NodeTypes AnyNodeTypes =
//         NodeTypes.Null | NodeTypes.Value | NodeTypes.Short | NodeTypes.Full | NodeTypes.Hash;

//     public const NodeTypes FullValueNodeTypes =
//         NodeTypes.Null | NodeTypes.Value | NodeTypes.Hash;

//     public const NodeTypes FullChildNodeTypes =
//         NodeTypes.Null | NodeTypes.Value | NodeTypes.Short | NodeTypes.Full | NodeTypes.Hash;

//     public const NodeTypes ShortValueNodeTypes =
//         NodeTypes.Value | NodeTypes.Short | NodeTypes.Full | NodeTypes.Hash;

//     public const NodeTypes HashEmbeddedNodeTypes =
//         NodeTypes.Value | NodeTypes.Short | NodeTypes.Full;

//     public static INode? Decode(IValue value, NodeTypes nodeTypes, ITable table)
//     {
//         if (value is List list)
//         {
//             if (list.Count == FullNode.MaximumIndex + 1)
//             {
//                 if ((nodeTypes & NodeTypes.Full) == NodeTypes.Full)
//                 {
//                     return DecodeFull(list, table);
//                 }
//             }
//             else if (list.Count == 2)
//             {
//                 if (list[0] is Binary)
//                 {
//                     if ((nodeTypes & NodeTypes.Short) == NodeTypes.Short)
//                     {
//                         return DecodeShort(list, table);
//                     }
//                 }
//                 else if (list[0] is Null)
//                 {
//                     if ((nodeTypes & NodeTypes.Value) == NodeTypes.Value)
//                     {
//                         return DecodeValue(list);
//                     }
//                 }
//             }
//         }
//         else if (value is Binary binary)
//         {
//             if ((nodeTypes & NodeTypes.Hash) == NodeTypes.Hash)
//             {
//                 return DecodeHash(binary, table);
//             }
//         }
//         else if (value is Null)
//         {
//             if ((nodeTypes & NodeTypes.Null) == NodeTypes.Null)
//             {
//                 return null;
//             }
//         }

//         throw new InvalidTrieNodeException($"Can't decode a node from value {value.Inspect()}");
//     }

//     internal static INode? Decode(IValue value, NodeTypes nodeTypes)
//         => Decode(value, nodeTypes, null!);

//     // The length and the first element are already checked.
//     private static ValueNode DecodeValue(List list) => new() { Value = list[1] };

//     // The length and the first element are already checked.
//     private static ShortNode DecodeShort(List list, ITable table)
//     {
//         if (list[0] is not Binary binary)
//         {
//             var message = "The first element of the given list is not a binary.";
//             throw new InvalidTrieNodeException(message);
//         }

//         if (Decode(list[1], ShortValueNodeTypes, table) is not { } node)
//         {
//             var message = $"Failed to decode a {nameof(ShortNode)} from given " +
//                           $"{nameof(list)}: {list}";
//             throw new NullReferenceException(message);
//         }

//         var key = new Nibbles(binary.ByteArray);
//         return new ShortNode { Key = key, Value = node };
//     }

//     // The length is already checked.
//     private static FullNode DecodeFull(List list, ITable table)
//     {
//         var builder = ImmutableDictionary.CreateBuilder<byte, INode>();
//         for (var i = 0; i < list.Count - 1; i++)
//         {
//             var node = Decode(list[i], FullChildNodeTypes, table);
//             if (node is not null)
//             {
//                 builder.Add((byte)i, node);
//             }
//         }

//         var value = Decode(list[FullNode.MaximumIndex], FullValueNodeTypes, table);

//         return new FullNode { Children = builder.ToImmutable(), Value = value };
//     }

//     private static HashNode DecodeHash(Binary binary, ITable table) => new()
//     {
//         Hash = new HashDigest<SHA256>(binary.ByteArray),
//         Table = table,
//     };
// }
