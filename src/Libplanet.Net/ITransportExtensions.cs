using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;
using Libplanet.Types.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Libplanet.Net;

public static class ITransportExtensions
{
    public static MessageEnvelope Send(this ITransport @this, Peer receiver, IMessage message)
        => @this.Send(receiver, message, null);

    public static void Send(this ITransport @this, ImmutableArray<Peer> receivers, IMessage message)
        => Parallel.ForEach(receivers, peer => @this.Send(peer, message, replyTo: null));

    public static async Task<T> SendAndWaitAsync<T>(
        this ITransport @this, Peer peer, IMessage message, CancellationToken cancellationToken)
        where T : IMessage
    {
        var request = @this.Send(peer, message, replyTo: null);
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = CancellationTask(@this, cancellationTokenSource);
        var resetEvent = new ManualResetEventSlim(false);
        MessageEnvelope? response = null;
        var handler = @this.MessageHandlers.Add<T>((m, e) =>
        {
            if (e.ReplyTo == request.Identity)
            {
                response = e;
                resetEvent.Set();
            }
        });
        using var _1 = new MessageHandlerScope(@this.MessageHandlers, handler);

        await Task.Run(() => resetEvent.Wait(cancellationTokenSource.Token), cancellationTokenSource.Token);
        if (response is null)
        {
            throw new UnreachableException("No response received before cancellation.");
        }

        return (T)response.Message;
    }

    public static async IAsyncEnumerable<T> SendAndWaitAsync<T>(
        this ITransport @this,
        Peer peer,
        IMessage message,
        Func<T, bool> isLast,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where T : IMessage
    {
        var request = @this.Send(peer, message, replyTo: null);
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = CancellationTask(@this, cancellationTokenSource);
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var handler = @this.MessageHandlers.Add<T>((m, e) =>
        {
            if (e.ReplyTo == request.Identity)
            {
                channel.Writer.TryWrite(e);
            }
        });
        using var _1 = new MessageHandlerScope(@this.MessageHandlers, handler);

        await foreach (var item in channel.Reader.ReadAllAsync(cancellationTokenSource.Token))
        {
            yield return (T)item.Message;
            if (isLast((T)item.Message))
            {
                yield break;
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
        await SendAndWaitAsync<PongMessage>(@this, peer, new PingMessage(), cancellationToken);
        return DateTimeOffset.UtcNow - dateTimeOffset;
    }

    internal static async Task<BlockHash[]> GetBlockHashesAsync(
        this ITransport @this, Peer peer, BlockHash blockHash, CancellationToken cancellationToken)
    {
        var request = new GetBlockHashesMessage { BlockHash = blockHash };
        var replyMessage = await SendAndWaitAsync<BlockHashMessage>(@this, peer, request, cancellationToken);
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
        return await SendAndWaitAsync<ChainStatusMessage>(@this, peer, requestMessage, cancellationToken);
    }

    internal static async IAsyncEnumerable<(Block, BlockCommit)> GetBlocksAsync(
        this ITransport @this,
        Peer peer,
        BlockHash[] blockHashes,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new GetBlockMessage { BlockHashes = [.. blockHashes] };
        var response = await @this.SendAndWaitAsync<BlockMessage>(peer, request, cancellationToken);
        for (var i = 0; i < response.Blocks.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return (response.Blocks[i], response.BlockCommits[i]);
        }

        // await foreach (var item in @this.SendAsync<BlockMessage>(peer, requestMessage, cancellationToken))
        // {
        //     for (var i = 0; i < item.Blocks.Length; i++)
        //     {
        //         cancellationToken.ThrowIfCancellationRequested();
        //         yield return (item.Blocks[i], item.BlockCommits[i]);
        //     }
        // }
    }

    public static async Task<ImmutableArray<Peer>> GetNeighborsAsync(
        this ITransport @this, Peer peer, Address target, CancellationToken cancellationToken)
    {
        var requestMessage = new GetPeerMessage { Target = target };
        var responseMessage = await @this.SendAndWaitAsync<PeerMessage>(
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

    internal static async ValueTask TransferAsync(this IReplyContext @this, EvidenceBase[] evidence)
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
        };
        await @this.NextAsync(replyMessage);
    }

    private static async Task CancellationTask(ITransport transport, CancellationTokenSource cancellationTokenSource)
    {
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            if (!@transport.IsRunning)
            {
                await cancellationTokenSource.CancelAsync();
            }
            else if (!await TaskUtility.TryDelay(100, cancellationTokenSource.Token))
            {
                break;
            }
        }
    }
}
