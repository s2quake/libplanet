using Libplanet.Data.Structures;
using Libplanet.Data.Structures.Nodes;

namespace Libplanet.Data.Tests.Structures.Nodes;

public class NullNodeTest
{
    [Fact]
    public void Test()
    {
        Assert.Equal(NullNode.Value, NullNode.Value with { });
        Assert.Empty(((INode)NullNode.Value).Children);
    }
}
