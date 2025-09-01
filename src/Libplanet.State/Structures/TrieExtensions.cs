using System.Diagnostics.CodeAnalysis;

namespace Libplanet.State.Structures;

public static class TrieExtensions
{
    public static bool TryGetValue<T>(this Trie @this, string key, [MaybeNullWhen(false)] out T value)
    {
        if (@this.TryGetValue(key, out object? v) && v is T t)
        {
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    public static T? GetValueOrDefault<T>(this Trie @this, string key)
        => @this.TryGetValue(key, out object? value) && value is T t ? t : default;

    public static T GetValueOrDefault<T>(this Trie @this, string key, T defaultValue)
        => @this.TryGetValue(key, out object? value) && value is T t ? t : defaultValue;
}
