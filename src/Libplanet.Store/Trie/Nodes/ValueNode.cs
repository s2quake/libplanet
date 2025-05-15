using Bencodex.Types;
using Libplanet.Serialization;

namespace Libplanet.Store.Trie.Nodes;

[Model(Version = 1)]
internal sealed record class ValueNode : INode
{
    IEnumerable<INode> INode.Children => [];

    [Property(0)]
    public required IValue Value { get; init; }

    public IValue ToBencodex() => new List(Null.Value, Value);

    public override int GetHashCode() => Value.GetHashCode();
}
