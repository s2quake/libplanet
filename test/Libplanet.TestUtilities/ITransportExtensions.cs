using Libplanet.Net;

namespace Libplanet.TestUtilities;

public static class ITransportExtensions
{
    public static MessageHandlingCounter<T> Counter<T>(this ITransport @this)
        where T : IMessage
        => new(@this);

    public static MessageHandlingCounter<T> Counter<T>(this ITransport @this, Func<T, bool> predicate)
        where T : IMessage
        => new(@this, predicate);
}
