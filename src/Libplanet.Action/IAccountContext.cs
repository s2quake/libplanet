using System.Diagnostics.CodeAnalysis;
using Libplanet.Store.Trie;

namespace Libplanet.Action;

public partial interface IAccountContext
{
    bool IsReadOnly { get; }

    object this[KeyBytes key] { get; set; }

    bool TryGetValue<T>(KeyBytes key, [MaybeNullWhen(false)] out T value);

    T GetValue<T>(KeyBytes key, T fallback);

    bool Contains(KeyBytes key);

    bool Remove(KeyBytes key);
}
