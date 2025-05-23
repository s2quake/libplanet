using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Libplanet.Types;

namespace Libplanet.Data.Structures;

public interface ITrie : IEnumerable<KeyValuePair<string, object>>
{
    INode Node { get; }

    HashDigest<SHA256> Hash { get; }

    bool IsCommitted { get; }

    object this[string key] { get; }

    ITrie Set(string key, object value);

    ITrie Remove(string key);

    INode GetNode(string key);

    bool TryGetNode(string key, [MaybeNullWhen(false)] out INode node);

    bool TryGetValue(string key, [MaybeNullWhen(false)] out object value);

    bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value)
    {
        if (TryGetValue(key, out object? v) && v is T t)
        {
            value = t;
            return true;
        }
        value = default;
        return false;
    }

    bool ContainsKey(string key);

    T? GetValueOrDefault<T>(string key)
        => TryGetValue(key, out object? value) && value is T t ? t : default;

    T GetValueOrDefault<T>(string key, T defaultValue)
        => TryGetValue(key, out object? value) && value is T t ? t : defaultValue;
}
