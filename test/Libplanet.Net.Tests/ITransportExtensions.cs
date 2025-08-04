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

    public static Task<MessageEnvelope> WaitAsync<T>(this ITransport @this, CancellationToken cancellationToken)
        where T : IMessage
        => WaitAsync<T>(@this, (_, _) => true, cancellationToken);

    public static async Task<MessageEnvelope> WaitAsync<T>(
        this ITransport @this, Func<T, bool> predicate, CancellationToken cancellationToken)
        where T : IMessage
        => await WaitAsync<T>(@this, (m, _) => predicate(m), cancellationToken);

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
}
