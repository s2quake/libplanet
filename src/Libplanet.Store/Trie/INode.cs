using System.Collections.Generic;
using System.Security.Cryptography;
using Bencodex;
using Bencodex.Types;
using Libplanet.Common;

namespace Libplanet.Store.Trie;

/// <summary>
/// A constituent unit of <see cref="Trie"/>.
/// </summary>
/// <seealso cref="FullNode"/>
/// <seealso cref="ShortNode"/>
/// <seealso cref="ValueNode"/>
/// <seealso cref="HashNode"/>
public interface INode
{
    private static readonly Codec _codec = new();

    IEnumerable<INode> Children { get; }

    HashDigest<SHA256> Hash => HashDigest<SHA256>.DeriveFrom(_codec.Encode(ToBencodex()));

    IValue ToBencodex();
}
