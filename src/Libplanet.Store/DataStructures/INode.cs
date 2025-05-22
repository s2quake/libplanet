namespace Libplanet.Store.DataStructures;

public interface INode
{
    IEnumerable<INode> Children { get; }
}
