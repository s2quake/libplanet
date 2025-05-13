using System.Diagnostics.CodeAnalysis;
using Libplanet.Types.Crypto;
using static Libplanet.Action.State.KeyConverters;

namespace Libplanet.Action.State;

public static class AccountExtensions
{
    public static object GetValue(this Account @this, string key) => @this.GetValue(ToStateKey(key));

    public static object GetValue(this Account @this, Address key) => @this.GetValue(ToStateKey(key));

    public static Account SetValue(this Account @this, string key, object value) => @this.SetValue(ToStateKey(key), value);

    public static Account SetValue(this Account @this, Address key, object value) => @this.SetValue(ToStateKey(key), value);

    public static object? GetValueOrDefault(this Account @this, string key) => @this.GetValueOrDefault(ToStateKey(key));

    public static object? GetValueOrDefault(this Account @this, Address key) => @this.GetValueOrDefault(ToStateKey(key));

    public static T GetValueOrFallback<T>(this Account @this, string key, T fallback)
        => @this.GetValueOrFallback(ToStateKey(key), fallback);

    public static T GetValueOrFallback<T>(this Account @this, Address key, T fallback)
        => @this.GetValueOrFallback(ToStateKey(key), fallback);

    public static bool ContainsKey(this Account @this, string key) => @this.ContainsKey(ToStateKey(key));

    public static bool ContainsKey(this Account @this, Address key) => @this.ContainsKey(ToStateKey(key));

    public static Account RemoveValue(this Account @this, string key) => @this.RemoveValue(ToStateKey(key));

    public static Account RemoveValue(this Account @this, Address key) => @this.RemoveValue(ToStateKey(key));

    public static bool TryGetValue(this Account @this, string key, [MaybeNullWhen(false)] out object value)
        => @this.TryGetValue(ToStateKey(key), out value);

    public static bool TryGetValue(this Account @this, Address key, [MaybeNullWhen(false)] out object value)
        => @this.TryGetValue(ToStateKey(key), out value);

    public static bool TryGetValue<T>(this Account @this, string key, [MaybeNullWhen(false)] out T value)
        => @this.TryGetValue(ToStateKey(key), out value);

    public static bool TryGetValue<T>(this Account @this, Address key, [MaybeNullWhen(false)] out T value)
        => @this.TryGetValue(ToStateKey(key), out value);
}
