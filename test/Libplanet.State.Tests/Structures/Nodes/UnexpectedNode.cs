using Libplanet.State.Structures;

namespace Libplanet.State.Tests.Structures.Nodes;

public sealed record class UnexpectedNode : INode
{
    public IEnumerable<INode> Children { get; } = [];
}
