using Libplanet.Serialization;

namespace Libplanet.Data.Structures.Nodes;

[Model(Version = 1, TypeName = "vnode")]
internal sealed partial record class ValueNode : INode
{
    [Property(0)]
    public required object Value { get; init; }

    IEnumerable<INode> INode.Children => [];

    public override int GetHashCode() => Value.GetHashCode();
}
