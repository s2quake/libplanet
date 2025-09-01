using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.State;

public static class AccountExtensions
{
    public static object GetValue(this Account @this, Address key) => @this.GetValue(key.ToString());

    public static Account SetValue(this Account @this, Address key, object value) => @this.SetValue(key.ToString(), value);

    public static object? GetValueOrDefault(this Account @this, Address key) => @this.GetValueOrDefault(key.ToString());

    public static T GetValueOrDefault<T>(this Account @this, Address key, T defaultValue)
        where T : notnull
        => @this.GetValueOrDefault(key.ToString(), defaultValue);

    public static T GetValueOrDefaultLenient<T>(this Account @this, string key, T defaultValue)
        where T : notnull
    {
        try
        {
            return @this.GetValueOrDefault(key, defaultValue);
        }
        catch (InvalidCastException)
        {
            return defaultValue;
        }
    }

    public static T GetValueOrDefaultLenient<T>(this Account @this, Address key, T defaultValue)
        where T : notnull
        => @this.GetValueOrDefaultLenient(key.ToString(), defaultValue);

    public static bool ContainsKey(this Account @this, Address key) => @this.ContainsKey(key.ToString());

    public static Account RemoveValue(this Account @this, Address key) => @this.RemoveValue(key.ToString());

    public static bool TryGetValue(this Account @this, Address key, [MaybeNullWhen(false)] out object value)
        => @this.TryGetValue(key.ToString(), out value);

    public static bool TryGetValue<T>(this Account @this, Address key, [MaybeNullWhen(false)] out T value)
        where T : notnull
        => @this.TryGetValue(key.ToString(), out value);

    public static bool TryGetValueLenient<T>(
        this Account @this, string key, T defaultValue, [MaybeNullWhen(false)] out T value)
        where T : notnull
    {
        try
        {
            value = @this.GetValueOrDefault(key, defaultValue);
            return true;
        }
        catch (InvalidCastException)
        {
            value = defaultValue;
            return false;
        }
    }

    public static bool TryGetValueLenient<T>(
        this Account @this, Address key, T defaultValue, [MaybeNullWhen(false)] out T value)
        where T : notnull
        => @this.TryGetValueLenient(key.ToString(), defaultValue, out value);
}
