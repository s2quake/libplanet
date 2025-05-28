using Libplanet.Data.Structures.Nodes;
using Libplanet.Serialization;
using Libplanet.Types.Tests;

namespace Libplanet.Data.Tests.Structures.Nodes;

public class ValueNodeTest
{
    [Fact]
    public void SerializationTest()
    {
        var expectedNode = new ValueNode
        {
            Value = RandomUtility.Word(),
        };
        var actualNode = ModelSerializer.Clone(expectedNode);
        Assert.Equal(expectedNode, actualNode);
    }
}
