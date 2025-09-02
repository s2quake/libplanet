using Libplanet.State.Structures;
using Libplanet.State.Structures.Nodes;
using Libplanet.Serialization;
using Libplanet.TestUtilities;
using Libplanet.Types;
using Libplanet.Data;

namespace Libplanet.State.Tests.Structures.Nodes;

public class FullNodeTest
{
    [Fact]
    public void Serialization()
    {
        var expectedNode = new FullNode
        {
            Children = ImmutableSortedDictionary<char, INode>.Empty,
        };
        var actualNode = ModelSerializer.Clone(expectedNode);
        Assert.Equal(expectedNode, actualNode);
    }

    [Fact]
    public void Children()
    {
        var node1 = new FullNode
        {
            Children = ImmutableSortedDictionary<char, INode>.Empty,
        };

        Assert.Empty(node1.Children);

        var valueNode2 = new ValueNode { Value = "child2" };
        var node2 = node1 with
        {
            Children = node1.Children.SetItem('A', valueNode2),
        };

        Assert.Single(node2.Children);
        Assert.Equal([valueNode2], node2.Children.Values);

        var valueNode3 = new ValueNode { Value = "child3" };
        var node3 = node2 with
        {
            Children = node2.Children.SetItem('0', valueNode3),
        };

        Assert.Equal([valueNode3, valueNode2], node3.Children.Values);

    }

    [Fact]
    public void INode_Children()
    {
        var node1 = new FullNode
        {
            Children = ImmutableSortedDictionary<char, INode>.Empty,
        };

        Assert.Equal([], ((INode)node1).Children);

        var childNodeA = new ValueNode { Value = "childA" };
        var node2 = node1 with
        {
            Children = node1.Children.SetItem('A', childNodeA),
        };

        Assert.Equal([childNodeA], ((INode)node2).Children);

        var childNode0 = new ValueNode { Value = "child0" };
        var node3 = node2 with
        {
            Children = node2.Children.SetItem('0', childNode0),
        };

        Assert.Equal([childNode0, childNodeA], ((INode)node3).Children);

        var node4 = node3 with { };
        Assert.Equal([childNode0, childNodeA], ((INode)node4).Children);
    }

    [Fact]
    public void GetChildOrDefault()
    {
        var node1 = new FullNode
        {
            Children = ImmutableSortedDictionary<char, INode>.Empty,
        };

        Assert.Null(node1.GetChildOrDefault('A'));

        var childNodeA = new ValueNode { Value = "childA" };
        var node2 = node1 with
        {
            Children = node1.Children.SetItem('A', childNodeA),
        };

        Assert.Equal(childNodeA, node2.GetChildOrDefault('A'));
        Assert.Null(node2.GetChildOrDefault('B'));
    }

    [Fact]
    public void SetChild()
    {
        var hashNode = new HashNode { Hash = default, StateIndex = [] };
        var node1 = new FullNode
        {
            Children = ImmutableSortedDictionary<char, INode>.Empty,
        };

        var childNodeA = new ValueNode { Value = "childA" };
        var node2 = node1.SetChild('A', childNodeA);

        Assert.Equal(childNodeA, node2.GetChildOrDefault('A'));
        Assert.Equal(childNodeA, node2.Children['A']);
        Assert.Single(node2.Children);

        Assert.Throws<ArgumentException>(() => node2.SetChild('B', hashNode));
        Assert.Throws<ArgumentException>(() => node2.SetChild('B', NullNode.Value));
        Assert.Throws<ArgumentException>(() => node2.SetChild('B', new UnexpectedNode()));
    }

    [Fact]
    public void RemoveChild()
    {
        var node1 = new FullNode
        {
            Children = ImmutableSortedDictionary<char, INode>.Empty,
        };

        Assert.Empty(node1.Children);

        var childNodeA = new ValueNode { Value = "childA" };
        var node2 = node1.SetChild('A', childNodeA);

        Assert.Single(node2.Children);
        Assert.Equal(childNodeA, node2.GetChildOrDefault('A'));

        var node3 = node2.RemoveChild('B');
        Assert.Equal(node2, node3);

        var node4 = node3.RemoveChild('A');
        Assert.Empty(node4.Children);
        Assert.Null(node4.GetChildOrDefault('A'));
    }

    [Fact]
    public void IValidatableObject_Validate()
    {
        var node1 = new FullNode
        {
            Children = ImmutableSortedDictionary<char, INode>.Empty,
        };
        ModelValidationUtility.Validate(node1);

        var node2 = new FullNode
        {
            Children = ImmutableSortedDictionary<char, INode>.Empty.SetItem('A', new ValueNode { Value = "childA" }),
        };
        ModelValidationUtility.Validate(node2);

        var node3 = new FullNode
        {
            Children = ImmutableSortedDictionary<char, INode>.Empty.SetItem('B', new FullNode
            {
                Children = ImmutableSortedDictionary<char, INode>.Empty
            }),
        };
        ModelValidationUtility.Validate(node3);

        var node4 = new FullNode
        {
            Children = ImmutableSortedDictionary<char, INode>.Empty.SetItem('C', new ShortNode
            {
                Key = "short",
                Value = new ValueNode { Value = "shortValue" },
            }),
        };
        ModelValidationUtility.Validate(node4);

        var hashNode = new HashNode { Hash = default, StateIndex = [] };
        var invalidNode1 = new FullNode
        {
            Children = ImmutableSortedDictionary<char, INode>.Empty.SetItem('A', hashNode),
        };
        ValidationTest.Throws(invalidNode1, nameof(FullNode.Children));

        var invalidNode2 = new FullNode
        {
            Children = ImmutableSortedDictionary<char, INode>.Empty.SetItem('B', new UnexpectedNode()),
        };
        ValidationTest.Throws(invalidNode2, nameof(FullNode.Children));

        var invalidNode3 = new FullNode
        {
            Children = ImmutableSortedDictionary<char, INode>.Empty.SetItem('C', NullNode.Value),
        };
        ValidationTest.Throws(invalidNode3, nameof(FullNode.Children));
    }
}
