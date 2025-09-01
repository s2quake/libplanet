namespace Libplanet.State.Structures;

public interface INode
{
    IEnumerable<INode> Children { get; }
}
