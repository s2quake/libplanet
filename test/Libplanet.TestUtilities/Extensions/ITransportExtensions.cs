using Libplanet.Net;

namespace Libplanet.TestUtilities.Extensions;

public static class ITransportExtensions
{
    public static MessageHandlingCounter<T> Counter<T>(this ITransport @this)
        where T : IMessage
        => new(@this);

    public static MessageHandlingCounter<T> Counter<T>(this ITransport @this, Func<T, bool> predicate)
        where T : IMessage
        => new(@this, predicate);

    public static void PostAfter(this ITransport @this, Peer receiver, IMessage message, int millisecondsDelay)
        => PostAfter(@this, receiver, message, TimeSpan.FromMilliseconds(millisecondsDelay));

    public static void PostAfter(this ITransport @this, Peer receiver, IMessage message, TimeSpan delay)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay);
            @this.Post(receiver, message);
        });
    }
}
