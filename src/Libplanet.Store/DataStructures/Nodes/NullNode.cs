namespace Libplanet.Store.DataStructures.Nodes;

internal sealed record class NullNode : INode
{
    private NullNode()
    {
    }

    public static NullNode Value { get; } = new();

    IEnumerable<INode> INode.Children => [];
}
