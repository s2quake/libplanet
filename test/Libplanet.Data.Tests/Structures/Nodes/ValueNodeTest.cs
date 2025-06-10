using Libplanet.Data.Structures;
using Libplanet.Data.Structures.Nodes;
using Libplanet.Serialization;
using Libplanet.TestUtilities;
using Libplanet.Types.Tests;

namespace Libplanet.Data.Tests.Structures.Nodes;

public class ValueNodeTest
{
    [Fact]
    public void Serialization()
    {
        var expectedNode = new ValueNode
        {
            Value = RandomUtility.Word(),
        };
        var actualNode = ModelSerializer.Clone(expectedNode);
        Assert.Equal(expectedNode, actualNode);
    }

    [Fact]
    public void Value()
    {
        var node1 = new ValueNode { Value = RandomUtility.Word() };
        Assert.Equal(node1.Value, node1.Value);
        Assert.NotEqual(node1.Value, RandomUtility.Word());

        var node2 = node1 with
        {
            Value = RandomUtility.Word(),
        };
        Assert.NotEqual(node1.Value, node2.Value);
        Assert.NotEqual(node1, node2);
    }

    [Fact]
    public void INode_Children()
    {
        var node1 = new ValueNode
        {
            Value = RandomUtility.Word(),
        };

        Assert.Equal([], ((INode)node1).Children);
    }

    [Fact]
    public void GetHashCodeTest()
    {
        var value1 = RandomUtility.Word();
        var value2 = RandomUtility.Word();
        var node1 = new ValueNode { Value = value1 };
        var node2 = new ValueNode { Value = value2 };

        Assert.Equal(value1.GetHashCode(), node1.GetHashCode());
        Assert.Equal(value2.GetHashCode(), node2.GetHashCode());

        var node3 = node2 with { Value = value1 };
        Assert.Equal(node1.GetHashCode(), node3.GetHashCode());
        Assert.Equal(value1.GetHashCode(), node3.GetHashCode());
    }
}
