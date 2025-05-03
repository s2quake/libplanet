using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Types;

namespace Libplanet.Store.Trie;

public interface ITrie : IEnumerable<KeyValuePair<KeyBytes, IValue>>
{
    INode Node { get; }

    HashDigest<SHA256> Hash { get; }

    bool IsCommitted { get; }

    IValue this[in KeyBytes key] { get; }

    IValue this[in ImmutableArray<byte> key] => this[new KeyBytes(key)];

    ITrie Set(in KeyBytes key, IValue value);

    ITrie Set(in ImmutableArray<byte> key, IValue value) => Set(new KeyBytes(key), value);

    ITrie Set(string key, IValue value) => Set((KeyBytes)key, value);

    ITrie Remove(in KeyBytes key);

    ITrie Remove(in ImmutableArray<byte> key) => Remove(new KeyBytes(key));

    INode GetNode(in Nibbles key);

    INode GetNode(in KeyBytes key);

    bool TryGetNode(in KeyBytes key, [MaybeNullWhen(false)] out INode node);

    bool TryGetValue(in KeyBytes key, [MaybeNullWhen(false)] out IValue value);

    bool TryGetValue<T>(in KeyBytes key, [MaybeNullWhen(false)] out T value)
        where T : IValue
    {
        if (TryGetValue(key, out IValue? v) && v is T t)
        {
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    bool ContainsKey(in KeyBytes key);

    T GetValue<T>(in KeyBytes key, T fallback)
        where T : IValue => TryGetValue(key, out IValue? value) && value is T t ? t : fallback;

    IEnumerable<(KeyBytes Path, IValue? TargetValue, IValue SourceValue)> Diff(ITrie other);
}
