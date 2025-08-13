using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net.Consensus;

public interface IMaj23Collection : IEnumerable<Maj23>
{
    Maj23 this[Address validator] { get; }

    int Count { get; }

    void Add(Maj23 maj23);

    bool TryGetValue(Address validator, [MaybeNullWhen(false)] out Maj23 value);

    bool Contains(Address validator);
}
