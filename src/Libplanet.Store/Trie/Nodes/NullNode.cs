using Bencodex.Types;

namespace Libplanet.Store.Trie.Nodes;

internal sealed record class NullNode : INode
{
    private NullNode()
    {
    }

    public static NullNode Value { get; } = new();

    IEnumerable<INode> INode.Children => [];

    public IValue ToBencodex() => Null.Value;

    public override int GetHashCode() => Null.Value.GetHashCode();
}
