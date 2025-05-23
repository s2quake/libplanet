using System.Diagnostics.CodeAnalysis;
using Libplanet.Action;

namespace Libplanet.Action;

public sealed class AccountStateContext(Account account, string name) : IAccountContext
{
    public string Name { get; } = name;

    public object this[string key]
    {
        get => account.GetValue(key);
        set => throw new NotSupportedException("Setting value is not supported.");
    }

    public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value) => account.TryGetValue(key, out value);

    public T GetValue<T>(string key, T fallback) => TryGetValue<T>(key, out var value) ? value : fallback;

    public bool Contains(string key) => account.GetValue(key) is not null;

    public bool Remove(string key) => throw new NotSupportedException("Removing state is not supported.");
}
