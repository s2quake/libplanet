using System.Collections.Generic;
using Bencodex.Types;

namespace Libplanet.Store.Trie.Nodes;

internal sealed record class ValueNode(IValue Value) : INode
{
    IEnumerable<INode> INode.Children => [];

    public IValue ToBencodex() => new List(Null.Value, Value);

    public override int GetHashCode() => Value.GetHashCode();
}
