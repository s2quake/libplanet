using Libplanet.State.Structures;
using Libplanet.State.Structures.Nodes;

namespace Libplanet.State.Tests.Structures.Nodes;

public class NullNodeTest
{
    [Fact]
    public void Test()
    {
        Assert.Equal(NullNode.Value, NullNode.Value with { });
        Assert.Empty(((INode)NullNode.Value).Children);
    }

    [Fact]
    public void UnexpectedNode()
    {
        var unexpectedNode = new UnexpectedNode();
        Assert.Empty(unexpectedNode.Children);
        Assert.Equal(unexpectedNode, unexpectedNode with { });
    }
}
