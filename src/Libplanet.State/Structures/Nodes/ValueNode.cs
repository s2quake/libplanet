using Libplanet.Serialization;

namespace Libplanet.State.Structures.Nodes;

[Model("vnode", Version = 1)]
internal sealed partial record class ValueNode : INode
{
    [Property(0)]
    public required object Value { get; init; }

    IEnumerable<INode> INode.Children => [];

    public override int GetHashCode() => Value.GetHashCode();
}
