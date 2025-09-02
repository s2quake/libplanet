using Libplanet.State.Structures;
using Libplanet.State.Structures.Nodes;
using Libplanet.Serialization;
using Libplanet.TestUtilities;
using Libplanet.Types;
using Libplanet.Data;

namespace Libplanet.State.Tests.Structures.Nodes;

public class ShortNodeTest
{
    [Fact]
    public void Serialization()
    {
        var expectedNode = new ShortNode
        {
            Key = RandomUtility.Word(),
            Value = new ValueNode { Value = RandomUtility.Word() },
        };
        var actualNode = ModelSerializer.Clone(expectedNode);
        Assert.Equal(expectedNode, actualNode);
    }

    [Fact]
    public void Key_Value()
    {
        var key = RandomUtility.Word();
        var valueNode = new ValueNode { Value = RandomUtility.Word() };
        var node = new ShortNode
        {
            Key = key,
            Value = valueNode,
        };

        Assert.Equal(key, node.Key);
        Assert.Equal(valueNode, node.Value);
    }

    [Fact]
    public void INode_Children()
    {
        var key = RandomUtility.Word();
        var valueNode = new ValueNode { Value = RandomUtility.Word() };
        var node = new ShortNode
        {
            Key = key,
            Value = valueNode,
        };

        Assert.Equal([valueNode], ((INode)node).Children);
    }

    [Fact]
    public void IValidatableObject_Validate()
    {
        var key = RandomUtility.Word();
        var valueNode = new ValueNode { Value = RandomUtility.Word() };
        var node1 = new ShortNode
        {
            Key = key,
            Value = valueNode,
        };
        ModelValidationUtility.Validate(node1);

        var node2 = node1 with
        {
            Value = new FullNode
            {
                Children = ImmutableSortedDictionary<char, INode>.Empty,
            },
        };
        ModelValidationUtility.Validate(node2);

        var node3 = node1 with
        {
            Value = new HashNode { Hash = default, StateIndex = [] },
        };
        ModelValidationUtility.Validate(node3);

        var invalidNode1 = new ShortNode
        {
            Key = string.Empty,
            Value = valueNode,
        };
        ValidationTest.Throws(invalidNode1, nameof(ShortNode.Key));

        var invalidNode2 = new ShortNode
        {
            Key = key,
            Value = node1,
        };
        ValidationTest.Throws(invalidNode2, nameof(ShortNode.Value));

        var invalidNode3 = new ShortNode
        {
            Key = key,
            Value = NullNode.Value,
        };
        ValidationTest.Throws(invalidNode3, nameof(ShortNode.Value));

        var invalidNode4 = new ShortNode
        {
            Key = key,
            Value = new UnexpectedNode(),
        };
        ValidationTest.Throws(invalidNode4, nameof(ShortNode.Value));
    }

}
