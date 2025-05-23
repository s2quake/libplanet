namespace Libplanet.Data.Structures;

public interface INode
{
    IEnumerable<INode> Children { get; }
}
