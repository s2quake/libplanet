using Libplanet.Net.Messages;

namespace Libplanet.Net.Tests;

public static class ITransportExtensions
{
    public static async Task WaitPingAsync(this ITransport @this, params Peer[] peers)
    {
        var tcs = new TaskCompletionSource<MessageEnvelope>();
        using var _ = @this.MessageRouter.Register<PingMessage>((message, messageEnvelope) =>
        {
            if (peers.Length is 0 || peers.Contains(messageEnvelope.Sender))
            {
                @this.Post(messageEnvelope.Sender, new PongMessage(), messageEnvelope.Identity);
                tcs.SetResult(messageEnvelope);
            }
        });

        await tcs.Task;
    }

    public static Task<MessageEnvelope> WaitAsync<T>(this ITransport @this)
        where T : IMessage
        => WaitAsync<T>(@this, cancellationToken: default);

    public static Task<MessageEnvelope> WaitAsync<T>(this ITransport @this, CancellationToken cancellationToken)
        where T : IMessage
        => WaitAsync<T>(@this, (_, _) => true, cancellationToken);

    public static Task<MessageEnvelope> WaitAsync<T>(
        this ITransport @this, TimeSpan timeout)
        where T : IMessage
        => WaitAsync<T>(@this).WaitAsync(timeout);

    public static Task<MessageEnvelope> WaitAsync<T>(
        this ITransport @this, TimeSpan timeout, CancellationToken cancellationToken)
        where T : IMessage
        => WaitAsync<T>(@this, cancellationToken).WaitAsync(timeout, cancellationToken);

    public static Task<MessageEnvelope> WaitAsync<T>(
        this ITransport @this, Func<T, bool> predicate)
        where T : IMessage
        => WaitAsync<T>(@this, predicate, cancellationToken: default);

    public static async Task<MessageEnvelope> WaitAsync<T>(
        this ITransport @this, Func<T, bool> predicate, CancellationToken cancellationToken)
        where T : IMessage
        => await WaitAsync<T>(@this, (m, _) => predicate(m), cancellationToken);

    public static Task<MessageEnvelope> WaitAsync<T>(
        this ITransport @this, Func<T, MessageEnvelope, bool> predicate)
        where T : IMessage
        => WaitAsync(@this, predicate, cancellationToken: default);

    public static async Task<MessageEnvelope> WaitAsync<T>(
        this ITransport @this, Func<T, MessageEnvelope, bool> predicate, CancellationToken cancellationToken)
        where T : IMessage
    {
        var tcs = new TaskCompletionSource<MessageEnvelope>();
        using var _1 = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        using var _2 = @this.MessageRouter.Register<T>((message, messageEnvelope) =>
        {
            if (predicate(message, messageEnvelope))
            {
                tcs.SetResult(messageEnvelope);
            }
        });

        return await tcs.Task;
    }

    public static Task<MessageEnvelope> WaitAsync<T>(
        this ITransport @this, Func<T, MessageEnvelope, bool> predicate, TimeSpan timeout)
        where T : IMessage
        => WaitAsync(@this, predicate).WaitAsync(timeout);

    public static Task<MessageEnvelope> WaitAsync<T>(
        this ITransport @this,
        Func<T, MessageEnvelope, bool> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        where T : IMessage
        => WaitAsync(@this, predicate, cancellationToken).WaitAsync(timeout, cancellationToken);
}
