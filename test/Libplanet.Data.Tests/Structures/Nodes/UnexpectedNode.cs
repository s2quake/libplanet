using Libplanet.Data.Structures;

namespace Libplanet.Data.Tests.Structures.Nodes;

public sealed record class UnexpectedNode : INode
{
    public IEnumerable<INode> Children => throw new NotImplementedException();
}
