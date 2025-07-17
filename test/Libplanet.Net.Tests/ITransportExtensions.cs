using System.Threading;
using System.Threading.Tasks;
using Libplanet.Extensions;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Tests;

public static class ITransportExtensions
{
    public static async Task WaitPingAsync(this ITransport @this, params Peer[] peers)
    {
        var manualResetEvent = new ManualResetEventSlim(false);
        var messageHandler = @this.MessageHandlers.Add<PingMessage>((message, messageEnvelope) =>
        {
            if (peers.Length is 0 || peers.Contains(messageEnvelope.Sender))
            {
                @this.Send(messageEnvelope.Sender, new PongMessage(), messageEnvelope.Identity);
                manualResetEvent.Set();
            }
        });

        try
        {
            await Task.Run(manualResetEvent.Wait, default);
        }
        finally
        {
            @this.MessageHandlers.Remove(messageHandler);
        }
    }

    public static Task WaitMessageAsync<T>(this ITransport @this, CancellationToken cancellationToken)
        where T : IMessage
        => WaitMessageAsync<T>(@this, (_, _) => true, cancellationToken);

    public static async Task WaitMessageAsync<T>(
        this ITransport @this, Func<T, bool> predicate, CancellationToken cancellationToken)
        where T : IMessage
        => await WaitMessageAsync<T>(@this, (m, _) => predicate(m), cancellationToken);

    public static async Task WaitMessageAsync<T>(
        this ITransport @this, Func<T, MessageEnvelope, bool> predicate, CancellationToken cancellationToken)
        where T : IMessage
    {
        var manualResetEvent = new ManualResetEventSlim(false);
        var messageHandler = @this.MessageHandlers.Add<T>((message, messageEnvelope) =>
        {
            if (predicate(message, messageEnvelope))
            {
                manualResetEvent.Set();
            }
        });

        try
        {
            await Task.Run(manualResetEvent.Wait, cancellationToken);
        }
        finally
        {
            @this.MessageHandlers.Remove(messageHandler);
        }
    }
}
