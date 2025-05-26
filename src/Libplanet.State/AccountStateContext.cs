using System.Diagnostics.CodeAnalysis;

namespace Libplanet.State;

public sealed class AccountStateContext(Account account, string name) : IAccountContext
{
    public string Name { get; } = name;

    public object this[string key]
    {
        get => account.GetValue(key);
        set => throw new NotSupportedException("Setting value is not supported.");
    }

    public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value) => account.TryGetValue(key, out value);

    public bool Contains(string key) => account.GetValue(key) is not null;

    public bool Remove(string key) => throw new NotSupportedException("Removing state is not supported.");
}
