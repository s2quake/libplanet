using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Store.Trie;

public interface IKeyValueStore : IDisposable
{
    IEnumerable<KeyBytes> Keys { get; }

    byte[] this[in KeyBytes key] { get; set; }

    void SetMany(IDictionary<KeyBytes, byte[]> values)
    {
        foreach (var (key, value) in values)
        {
            this[key] = value;
        }
    }

    byte[] Get(in KeyBytes key) => this[key];

    void Set(in KeyBytes key, byte[] value) => this[key] = value;

    bool Remove(in KeyBytes key);

    void RemoveMany(IEnumerable<KeyBytes> keys)
    {
        foreach (var key in keys)
        {
            Remove(key);
        }
    }

    bool ContainsKey(in KeyBytes key);

    bool TryGetValue(in KeyBytes key, [MaybeNullWhen(false)] out byte[] value)
    {
        if (ContainsKey(key))
        {
            value = this[key];
            return true;
        }

        value = null;
        return false;
    }
}
