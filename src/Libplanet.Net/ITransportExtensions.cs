using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Libplanet.Net.Messages;
using Libplanet.Types;

namespace Libplanet.Net;

public static class ITransportExtensions
{
    public static MessageEnvelope Post(this ITransport @this, Peer receiver, IMessage message)
        => @this.Post(receiver, message, replyTo: null);

    public static void Post(this ITransport @this, ImmutableArray<Peer> receivers, IMessage message)
        => Post(@this, receivers, message, replyTo: null);

    public static void Post(this ITransport @this, ImmutableArray<Peer> receivers, IMessage message, Guid? replyTo)
        => Parallel.ForEach(receivers, peer => @this.Post(peer, message, replyTo));

    public static async Task<T> SendAsync<T>(
        this ITransport @this, Peer peer, IMessage message, CancellationToken cancellationToken)
        where T : IMessage
    {
        var request = @this.Post(peer, message, replyTo: null);
        using var timeoutCancellationTokenSource = new CancellationTokenSource(request.ReplyTimeout);
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            @this.StoppingToken, cancellationToken, timeoutCancellationTokenSource.Token);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var _1 = cancellationTokenSource.Token.Register(() => tcs.TrySetCanceled(cancellationTokenSource.Token));
        using var _2 = @this.MessageRouter.Register<T>((m, e) =>
        {
            if (e.ReplyTo == request.Identity)
            {
                tcs.SetResult((T)e.Message);
            }
        });

        try
        {
            return await tcs.Task;
        }
        catch (OperationCanceledException e) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            throw new TimeoutException($"No response received within {request.ReplyTimeout.TotalSeconds} seconds.", e);
        }
        catch (OperationCanceledException e) when (@this.StoppingToken.IsCancellationRequested)
        {
            throw new OperationCanceledException($"{@this} has been stopped.", e, @this.StoppingToken);
        }
        catch (TaskCanceledException e)
        {
            throw new OperationCanceledException(e.Message, e, e.CancellationToken);
        }
    }

    public static async IAsyncEnumerable<T> SendAsync<T>(
        this ITransport @this,
        Peer peer,
        IMessage message,
        Func<T, bool> isLast,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where T : IMessage
    {
        var request = @this.Post(peer, message, replyTo: null);
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        using var _1 = @this.MessageRouter.Register<T>((m, e) =>
        {
            if (e.ReplyTo == request.Identity)
            {
                channel.Writer.TryWrite(e);
            }
        });

        while (true)
        {
            var response = await ReadAsync();
            yield return (T)response.Message;
            if (isLast((T)response.Message))
            {
                break;
            }
        }

        async Task<MessageEnvelope> ReadAsync()
        {
            using var timeoutCancellationTokenSource = new CancellationTokenSource(request.ReplyTimeout);
            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                @this.StoppingToken, cancellationToken, timeoutCancellationTokenSource.Token);

            try
            {
                return await channel.Reader.ReadAsync(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException e) when (timeoutCancellationTokenSource.IsCancellationRequested)
            {
                throw new TimeoutException($"No response received within {request.ReplyTimeout.TotalSeconds} seconds.", e);
            }
            catch (OperationCanceledException e) when (@this.StoppingToken.IsCancellationRequested)
            {
                throw new OperationCanceledException($"{@this} has been stopped.", e, @this.StoppingToken);
            }
        }
    }

    public static async Task<TimeSpan> PingAsync(this ITransport @this, Peer peer, CancellationToken cancellationToken)
    {
        if (@this.Peer.Equals(peer))
        {
            throw new InvalidOperationException("Cannot ping self");
        }

        var dateTimeOffset = DateTimeOffset.UtcNow;
        await SendAsync<PongMessage>(@this, peer, new PingMessage(), cancellationToken);
        return DateTimeOffset.UtcNow - dateTimeOffset;
    }

    public static void Pong(this ITransport @this, MessageEnvelope replyTo)
        => @this.Post(replyTo.Sender, new PongMessage(), replyTo.Identity);

    internal static async Task<ImmutableArray<BlockHash>> GetBlockHashesAsync(
        this ITransport @this, Peer peer, BlockHash blockHash, CancellationToken cancellationToken)
    {
        var request = new BlockHashRequestMessage { BlockHash = blockHash };
        var response = await SendAsync<BlockHashResponseMessage>(@this, peer, request, cancellationToken);
        var blockHashes = response.BlockHashes;
        if (blockHashes.Length > 0 && blockHash != blockHashes[0])
        {
            throw new InvalidOperationException(
                $"Expected the first block hash to be {blockHash}, but got {blockHashes[0]} instead.");
        }

        return [.. blockHashes];
    }

    internal static async Task<BlockchainStateResponseMessage> GetBlockchainStateAsync(
        this ITransport @this, Peer peer, CancellationToken cancellationToken)
    {
        var request = new BlockchainStateRequestMessage();
        return await SendAsync<BlockchainStateResponseMessage>(@this, peer, request, cancellationToken);
    }

    internal static async IAsyncEnumerable<(Block, BlockCommit)> GetBlocksAsync(
        this ITransport @this,
        Peer peer,
        BlockHash[] blockHashes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new BlockRequestMessage { BlockHashes = [.. blockHashes] };
        var isLast = new Func<BlockResponseMessage, bool>(response => response.IsLast);
        await foreach (var response in @this.SendAsync(peer, request, isLast, cancellationToken))
        {
            for (var i = 0; i < response.Blocks.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return (response.Blocks[i], response.BlockCommits[i]);
            }
        }
    }

    internal static async IAsyncEnumerable<Transaction> GetTransactionsAsync(
        this ITransport @this,
        Peer peer,
        TxId[] txIds,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new TransactionRequestMessage { TxIds = [.. txIds] };
        var isLast = new Func<TransactionResponseMessage, bool>(response => response.IsLast);
        await foreach (var response in @this.SendAsync(peer, request, isLast, cancellationToken))
        {
            for (var i = 0; i < response.Transactions.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return response.Transactions[i];
            }
        }
    }

    public static async Task<ImmutableArray<Peer>> GetNeighborsAsync(
        this ITransport @this, Peer peer, Address target, CancellationToken cancellationToken)
    {
        var request = new PeerRequestMessage { Target = target };
        var response = await @this.SendAsync<PeerResponseMessage>(peer, request, cancellationToken);
        return response.Peers;
    }

    internal static void PostBlock(this ITransport @this, Peer peer, Blockchain blockchain, Block block)
    {
        var message = new BlockSummaryMessage
        {
            BlockSummary = block,
            GenesisBlockHash = blockchain.Genesis.BlockHash,
        };
        @this.Post(peer, message);
    }
}
