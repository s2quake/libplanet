using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet.Store.Trie.Nodes;

internal sealed record class NullNode : INode
{
    private NullNode()
    {
    }

    public static NullNode Value { get; } = new();

    IEnumerable<INode> INode.Children => [];

    public HashDigest<SHA256> Hash => default;
}
