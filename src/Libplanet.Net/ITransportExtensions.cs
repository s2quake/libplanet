using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;

namespace Libplanet.Net;

public static class ITransportExtensions
{
    public static async Task PingAsync(this ITransport @this, Peer peer, CancellationToken cancellationToken)
    {
        if (@this.Peer.Equals(peer))
        {
            throw new InvalidOperationException("Cannot ping self");
        }

        var reply = await @this.SendMessageAsync(peer, new PingMessage(), cancellationToken);
        if (reply.Message is not PongMessage)
        {
            throw new InvalidOperationException($"Expected pong, but received {reply.Message}.");
        }
        else if (reply.Peer.Address.Equals(@this.Peer.Address))
        {
            throw new InvalidOperationException("Cannot receive pong from self");
        }
    }
}
