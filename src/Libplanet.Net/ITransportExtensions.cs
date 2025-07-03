using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Serialization;
using Libplanet.Types;

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

    public static void Pong(this ITransport @this, MessageEnvelope messageEnvelope)
        => @this.ReplyMessage(messageEnvelope.Identity, new PongMessage());

    internal static async Task<BlockHash[]> GetBlockHashes(
        this ITransport @this, Peer peer, BlockHash blockHash, CancellationToken cancellationToken)
    {
        var request = new GetBlockHashesMessage { BlockHash = blockHash };
        var reply = await @this.SendMessageAsync(peer, request, cancellationToken);
        var blockHashes = reply.Message is BlockHashesMessage replyMessage ? replyMessage.Hashes : [];
        if (blockHashes.Length > 0 && blockHash == blockHashes[0])
        {
            return [.. blockHashes];
        }

        return [];
    }

    internal static async Task<ChainStatusMessage> GetChainStatusAsync(
        this ITransport @this, Peer peer, CancellationToken cancellationToken)
    {
        var requestMessage = new GetChainStatusMessage();
        var reply = await @this.SendMessageAsync(peer, requestMessage, cancellationToken);
        if (reply.Message is not ChainStatusMessage chainStatus)
        {
            throw new InvalidMessageContractException(
                $"Expected a {nameof(ChainStatusMessage)} message as a response of " +
                $"{nameof(GetChainStatusMessage)}, but got a {reply.Message.GetType().Name} " +
                $"message instead: {reply.Message}");
        }

        return chainStatus;
    }
}
