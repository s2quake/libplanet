using System.Diagnostics.CodeAnalysis;

namespace Libplanet.State;

public partial interface IAccountContext
{
    object this[string key] { get; set; }

    bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value);

    bool Contains(string key);

    bool Remove(string key);
}
