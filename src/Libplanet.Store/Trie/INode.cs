using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Types;

namespace Libplanet.Store.Trie;

public interface INode
{
    private static readonly Codec _codec = new();

    IEnumerable<INode> Children { get; }

    HashDigest<SHA256> Hash => HashDigest<SHA256>.Create(_codec.Encode(ToBencodex()));

    IValue ToBencodex();
}
