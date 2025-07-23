using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net;

internal partial interface IBucket : IEnumerable<PeerState>
{
    int Count { get; }

    int Capacity { get; }

    PeerState Newest { get; }

    PeerState Oldest { get; }

    PeerState this[Address address] { get; }

    bool Contains(Address address);

    bool TryGetValue(Address address, [MaybeNullWhen(false)] out PeerState value);
}
