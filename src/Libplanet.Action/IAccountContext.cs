using System.Diagnostics.CodeAnalysis;
using Libplanet.Crypto;

namespace Libplanet.Action;

public interface IAccountContext
{
    bool IsReadOnly { get; }

    object this[Address address] { get; set; }

    bool TryGetValue<T>(Address address, [MaybeNullWhen(false)] out T value);

    T GetValue<T>(Address address, T fallback);

    bool Contains(Address address);

    bool Remove(Address address);
}
