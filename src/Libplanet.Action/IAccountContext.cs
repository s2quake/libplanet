using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Action;

public partial interface IAccountContext
{
    object this[string key] { get; set; }

    bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value);

    T GetValue<T>(string key, T fallback);

    bool Contains(string key);

    bool Remove(string key);
}
