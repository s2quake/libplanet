using Libplanet.Serialization;

namespace Libplanet.Store.Trie.Nodes;

[Model(Version = 1)]
internal sealed record class ValueNode : INode
{
    IEnumerable<INode> INode.Children => [];

    [Property(0)]
    public required object Value { get; init; }

    public override int GetHashCode() => Value.GetHashCode();
}
