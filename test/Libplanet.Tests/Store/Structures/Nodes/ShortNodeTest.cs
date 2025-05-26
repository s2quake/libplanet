using Libplanet.Data.Structures.Nodes;
using Libplanet.Serialization;

namespace Libplanet.Tests.Store.Structures.Nodes;

public class ShortNodeTest
{
    [Fact]
    public void SerializationTest()
    {
        var expectedNode = new ShortNode
        {
            Key = RandomUtility.Word(),
            Value = new ValueNode { Value = RandomUtility.Word() },
        };
        var actualNode = ModelSerializer.Clone(expectedNode);
        Assert.Equal(expectedNode, actualNode);
    }
}
