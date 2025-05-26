using Libplanet.Data.Structures;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Serialization;

namespace Libplanet.Tests.Store.Structures.Nodes;

public class FullNodeTest
{
    [Fact]
    public void SerializationTest()
    {
        var expectedNode = new FullNode
        {
            Children = ImmutableSortedDictionary<char, INode>.Empty,
            Value = new ValueNode { Value = "123" },
        };
        var actualNode = ModelSerializer.Clone(expectedNode);
        Assert.Equal(expectedNode, actualNode);
    }
}
