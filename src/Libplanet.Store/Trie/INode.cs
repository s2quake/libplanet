namespace Libplanet.Store.Trie;

public interface INode
{
    IEnumerable<INode> Children { get; }
}
