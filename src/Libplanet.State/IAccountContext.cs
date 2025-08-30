using System.Diagnostics.CodeAnalysis;

namespace Libplanet.State;

public partial interface IAccountContext
{
    object this[string key] { get; set; }

    /// <exception cref="InvalidCastException">
    /// Thrown when the value stored at the specified key cannot be cast to the specified type.
    /// </exception>
    bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value) where T : notnull;

    bool Contains(string key);

    bool Remove(string key);
}
