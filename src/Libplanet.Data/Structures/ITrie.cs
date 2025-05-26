using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet.Data.Structures;

public partial interface ITrie : IEnumerable<KeyValuePair<string, object>>
{
    INode Node { get; }

    HashDigest<SHA256> Hash { get; }

    bool IsCommitted { get; }

    object this[string key] { get; }

    ITrie Set(string key, object value);

    ITrie Remove(string key);

    bool TryGetValue(string key, [MaybeNullWhen(false)] out object value);

    bool ContainsKey(string key);

    INode GetNode(string key);

    bool TryGetNode(string key, [MaybeNullWhen(false)] out INode node);
}
