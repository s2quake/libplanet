using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet.Store.Trie;

public interface INode
{
    IEnumerable<INode> Children { get; }

    HashDigest<SHA256> Hash => HashDigest<SHA256>.Create(Serialize());

    byte[] Serialize();
}
