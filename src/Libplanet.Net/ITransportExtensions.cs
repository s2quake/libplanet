using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net;

public static class ITransportExtensions
{
    public static async IAsyncEnumerable<T> SendAsync<T>(
        this ITransport @this,
        Peer peer,
        IMessage message,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where T : IMessage
    {
        await foreach (var item in @this.SendAsync(peer, message, cancellationToken))
        {
            if (item is T typedMessage)
            {
                yield return typedMessage;
            }
            else
            {
                throw new InvalidMessageContractException(
                    $"Expected a {typeof(T).Name} message as a response of " +
                    $"{message.GetType().Name}, but got a {item.GetType().Name} " +
                    $"message instead: {item}");
            }
        }
    }

    public static async Task<T> SendForSingleAsync<T>(
        this ITransport @this, Peer peer, IMessage message, CancellationToken cancellationToken)
        where T : IMessage
    {
        var replyMessag = await @this.SendAsync<T>(peer, message, cancellationToken).FirstAsync(cancellationToken);
        if (replyMessag is not T typedMessage)
        {
            throw new InvalidMessageContractException(
                $"Expected a {typeof(T).Name} message as a response of " +
                $"{message.GetType().Name}, but got a {replyMessag.GetType().Name} " +
                $"message instead: {replyMessag}");
        }

        return typedMessage;
    }

    public static async Task<TimeSpan> PingAsync(this ITransport @this, Peer peer, CancellationToken cancellationToken)
    {
        if (@this.Peer.Equals(peer))
        {
            throw new InvalidOperationException("Cannot ping self");
        }

        var dateTimeOffset = DateTimeOffset.UtcNow;
        await SendForSingleAsync<PongMessage>(@this, peer, new PingMessage(), cancellationToken);
        return DateTimeOffset.UtcNow - dateTimeOffset;
    }

    internal static async Task<BlockHash[]> GetBlockHashesAsync(
        this ITransport @this, Peer peer, BlockHash blockHash, CancellationToken cancellationToken)
    {
        var request = new GetBlockHashesMessage { BlockHash = blockHash };
        var replyMessage = await SendForSingleAsync<BlockHashMessage>(@this, peer, request, cancellationToken);
        var blockHashes = replyMessage.BlockHashes;
        if (blockHashes.Length > 0 && blockHash != blockHashes[0])
        {
            throw new InvalidMessageContractException(
                $"Expected the first block hash to be {blockHash}, but got {blockHashes[0]} instead.");
        }

        return [.. blockHashes];
    }

    internal static async Task<ChainStatusMessage> GetChainStatusAsync(
        this ITransport @this, Peer peer, CancellationToken cancellationToken)
    {
        var requestMessage = new GetChainStatusMessage();
        return await SendForSingleAsync<ChainStatusMessage>(@this, peer, requestMessage, cancellationToken);
    }

    internal static async IAsyncEnumerable<(Block, BlockCommit)> GetBlocksAsync(
        this ITransport @this,
        Peer peer,
        BlockHash[] blockHashes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var requestMessage = new GetBlockMessage { BlockHashes = [.. blockHashes] };

        await foreach (var item in @this.SendAsync<BlockMessage>(peer, requestMessage, cancellationToken))
        {
            for (var i = 0; i < item.Blocks.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return (item.Blocks[i], item.BlockCommits[i]);
            }
        }
    }

    public static async Task<ImmutableArray<Peer>> GetNeighborsAsync(
        this ITransport @this, Peer peer, Address target, CancellationToken cancellationToken)
    {
        var requestMessage = new GetPeerMessage { Target = target };
        var responseMessage = await @this.SendForSingleAsync<PeerMessage>(
            peer, requestMessage, cancellationToken);
        return responseMessage.Peers;
    }

    internal static async ValueTask TransferAsync(this IReplyContext @this, Transaction[] transactions)
    {
        var replyMessage = new TransactionMessage
        {
            Transactions = [.. transactions],
        };
        await @this.NextAsync(replyMessage);
    }

    internal static async ValueTask TransferAsync(this IReplyContext @this,  EvidenceBase[] evidence)
    {
        var replyMessage = new EvidenceMessage
        {
            Evidence = [.. evidence],
        };
        await @this.NextAsync(replyMessage);
    }

    internal static async ValueTask TransferAsync(
        this IReplyContext @this, Block[] blocks, BlockCommit[] blockCommits, bool hasNext = false)
    {
        var replyMessage = new BlockMessage
        {
            Blocks = [.. blocks],
            BlockCommits = [.. blockCommits],
            HasNext = hasNext,
        };
        await @this.NextAsync(replyMessage);
    }
}
