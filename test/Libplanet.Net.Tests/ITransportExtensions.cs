using System.Threading;
using System.Threading.Tasks;
using Libplanet.Extensions;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Tests;

public static class ITransportExtensions
{
    public static async Task WaitPingAsync(this ITransport @this, params Peer[] peers)
    {
        await @this.Process.WaitAsync(replyContext =>
        {
            if (replyContext.Message is PingMessage && (peers.Length is 0 || peers.Contains(replyContext.Sender)))
            {
                replyContext.Pong();
                return true;
            }

            return false;

        }, default);
    }

    public static Task WaitMessageAsync<T>(this ITransport @this, CancellationToken cancellationToken)
        => WaitMessageAsync<T>(@this, m => true, cancellationToken);

    public static async Task WaitMessageAsync<T>(
        this ITransport @this, Func<T, bool> predicate, CancellationToken cancellationToken)
    {
        await @this.Process.WaitAsync(Predicate, cancellationToken);

        bool Predicate(IReplyContext replyContext) => replyContext.Message is T message && predicate(message);
    }

    public static IDisposable RegisterPingHandler(this ITransport @this) => @this.RegisterPingHandler([]);

    public static IDisposable RegisterPingHandler(this ITransport @this, ImmutableArray<Peer> peers)
    {
        return @this.Process.Subscribe(replyContext =>
        {
            if (replyContext.Message is PingMessage && (peers.Length is 0 || peers.Contains(replyContext.Sender)))
            {
                replyContext.Pong();
            }
        });
    }
}
