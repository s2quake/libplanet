using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Serialization;

namespace Libplanet.Store.Trie.Nodes;

public static class NodeDecoder
{
    public const NodeTypes AnyNodeTypes =
        NodeTypes.Null | NodeTypes.Value | NodeTypes.Short | NodeTypes.Full | NodeTypes.Hash;

    public const NodeTypes FullValueNodeTypes =
        NodeTypes.Null | NodeTypes.Value | NodeTypes.Hash;

    public const NodeTypes FullChildNodeTypes =
        NodeTypes.Null | NodeTypes.Value | NodeTypes.Short | NodeTypes.Full | NodeTypes.Hash;

    public const NodeTypes ShortValueNodeTypes =
        NodeTypes.Value | NodeTypes.Short | NodeTypes.Full | NodeTypes.Hash;

    public const NodeTypes HashEmbeddedNodeTypes =
        NodeTypes.Value | NodeTypes.Short | NodeTypes.Full;

    public static INode? Decode(IValue value, NodeTypes nodeTypes, IKeyValueStore keyValueStore)
    {
        if (value is List list)
        {
            if (list.Count == FullNode.MaximumIndex + 1)
            {
                if ((nodeTypes & NodeTypes.Full) == NodeTypes.Full)
                {
                    return DecodeFull(list, keyValueStore);
                }
            }
            else if (list.Count == 2)
            {
                if (list[0] is Binary)
                {
                    if ((nodeTypes & NodeTypes.Short) == NodeTypes.Short)
                    {
                        return DecodeShort(list, keyValueStore);
                    }
                }
                else if (list[0] is Null)
                {
                    if ((nodeTypes & NodeTypes.Value) == NodeTypes.Value)
                    {
                        return DecodeValue(list);
                    }
                }
            }
        }
        else if (value is Binary binary)
        {
            if ((nodeTypes & NodeTypes.Hash) == NodeTypes.Hash)
            {
                return DecodeHash(binary, keyValueStore);
            }
        }
        else if (value is Null)
        {
            if ((nodeTypes & NodeTypes.Null) == NodeTypes.Null)
            {
                return null;
            }
        }

        throw new InvalidTrieNodeException($"Can't decode a node from value {value.Inspect()}");
    }

    internal static INode? Decode(IValue value, NodeTypes nodeTypes)
        => Decode(value, nodeTypes, null!);

    // The length and the first element are already checked.
    private static ValueNode DecodeValue(List list) => new(list[1]);

    // The length and the first element are already checked.
    private static ShortNode DecodeShort(List list, IKeyValueStore keyValueStore)
    {
        if (list[0] is not Binary binary)
        {
            var message = "The first element of the given list is not a binary.";
            throw new InvalidTrieNodeException(message);
        }

        if (Decode(list[1], ShortValueNodeTypes, keyValueStore) is not { } node)
        {
            var message = $"Failed to decode a {nameof(ShortNode)} from given " +
                          $"{nameof(list)}: {list}";
            throw new NullReferenceException(message);
        }

        var key = new Nibbles(binary.ByteArray);
        return new ShortNode(key, node);
    }

    // The length is already checked.
    private static FullNode DecodeFull(List list, IKeyValueStore keyValueStore)
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, INode>();
        for (var i = 0; i < list.Count - 1; i++)
        {
            var node = Decode(list[i], FullChildNodeTypes, keyValueStore);
            if (node is not null)
            {
                builder.Add((byte)i, node);
            }
        }

        var value = Decode(list[FullNode.MaximumIndex], FullValueNodeTypes, keyValueStore);

        return new FullNode(builder.ToImmutable(), value);
    }

    private static HashNode DecodeHash(Binary binary, IKeyValueStore keyValueStore)
        => new(ModelSerializer.Deserialize<HashDigest<SHA256>>(binary))
        {
            KeyValueStore = keyValueStore,
        };
}
