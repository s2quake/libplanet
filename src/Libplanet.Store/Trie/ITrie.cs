using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet.Store.Trie;

public interface ITrie : IEnumerable<KeyValuePair<KeyBytes, object>>
{
    INode Node { get; }

    HashDigest<SHA256> Hash { get; }

    bool IsCommitted { get; }

    object this[in KeyBytes key] { get; }

    ITrie Set(in KeyBytes key, object value);

    ITrie Remove(in KeyBytes key);

    INode GetNode(in Nibbles key);

    INode GetNode(in KeyBytes key);

    bool TryGetNode(in Nibbles key, [MaybeNullWhen(false)] out INode node);

    bool TryGetNode(in KeyBytes key, [MaybeNullWhen(false)] out INode node);

    bool TryGetValue(in KeyBytes key, [MaybeNullWhen(false)] out object value);

    bool TryGetValue<T>(in KeyBytes key, [MaybeNullWhen(false)] out T value)
    {
        if (TryGetValue(key, out object? v) && v is T t)
        {
            value = t;
            return true;
        }
        value = default;
        return false;
    }

    bool ContainsKey(in KeyBytes key);

    T? GetValueOrDefault<T>(in KeyBytes key)
        => TryGetValue(key, out object? value) && value is T t ? t : default;

    T GetValueOrDefault<T>(in KeyBytes key, T defaultValue)
        => TryGetValue(key, out object? value) && value is T t ? t : defaultValue;
}
