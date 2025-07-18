using System.Diagnostics;
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
                @this.Post(messageEnvelope.Sender, new PongMessage(), messageEnvelope.Identity);
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

    public static Task<MessageEnvelope> WaitMessageAsync<T>(this ITransport @this, CancellationToken cancellationToken)
        where T : IMessage
        => WaitMessageAsync<T>(@this, (_, _) => true, cancellationToken);

    public static async Task<MessageEnvelope> WaitMessageAsync<T>(
        this ITransport @this, Func<T, bool> predicate, CancellationToken cancellationToken)
        where T : IMessage
        => await WaitMessageAsync<T>(@this, (m, _) => predicate(m), cancellationToken);

    public static async Task<MessageEnvelope> WaitMessageAsync<T>(
        this ITransport @this, Func<T, MessageEnvelope, bool> predicate, CancellationToken cancellationToken)
        where T : IMessage
    {
        MessageEnvelope? returnValue = null;
        var manualResetEvent = new ManualResetEventSlim(false);
        var messageHandler = @this.MessageHandlers.Add<T>((message, messageEnvelope) =>
        {
            if (predicate(message, messageEnvelope))
            {
                returnValue = messageEnvelope;
                manualResetEvent.Set();
            }
        });

        try
        {
            await Task.Run(manualResetEvent.Wait, cancellationToken);
            return returnValue ?? throw new UnreachableException("No message received before cancellation.");
        }
        finally
        {
            @this.MessageHandlers.Remove(messageHandler);
        }
    }
}
