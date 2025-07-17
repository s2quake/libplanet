using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;
using NetMQ;

namespace Libplanet.Net.Transports;

public sealed class NetMQTransport(ISigner signer, TransportOptions options)
    : ServiceBase, ITransport
{
    private readonly NetMQRouterSocket _router = new(signer.Address, options.Host, options.Port);
    private readonly NetMQQueue<MessageResponse> _responseQueue = new();
    private readonly TransportOptions _options = ValidationUtility.ValidateAndReturn(options);
    private NetMQRequestWorker? _requestWorker;
    private NetMQPoller? _poller;

    public NetMQTransport(ISigner signer)
        : this(signer, new TransportOptions())
    {
    }

    public MessageHandlerCollection MessageHandlers { get; } = [];

    public Peer Peer => _router.Peer;

    public Protocol Protocol => _options.Protocol;

    // public async IAsyncEnumerable<IMessage> SendAsync(
    //     Peer receiver, IMessage message, [EnumeratorCancellation] CancellationToken cancellationToken)
    // {
    //     ObjectDisposedException.ThrowIf(IsDisposed, this);

    //     using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);
    //     var messageEnvelope = new MessageEnvelope
    //     {
    //         Identity = Guid.NewGuid(),
    //         Message = message,
    //         Protocol = _options.Protocol,
    //         Sender = Peer,
    //         Timestamp = DateTimeOffset.UtcNow,
    //     };
    //     var channel = await WriteAsync(receiver, messageEnvelope, cancellationTokenSource.Token);
    //     var hasNext = true;
    //     while (hasNext)
    //     {
    //         var response = await ReadAsync(channel, cancellationTokenSource.Token);
    //         hasNext = response.HasNext;
    //         yield return response.MessageEnvelope.Message;
    //     }

    //     channel.Writer.TryComplete();
    // }

    public MessageEnvelope Send(Peer receiver, IMessage message, Guid? replyTo = null)
    {
        if (_requestWorker is null)
        {
            throw new InvalidOperationException("Transport is not running");
        }

        using var cancellationTokenSource = CreateCancellationTokenSource();

        var messageEnvelope = new MessageEnvelope
        {
            Identity = Guid.NewGuid(),
            Message = message,
            Protocol = _options.Protocol,
            Sender = Peer,
            Timestamp = DateTimeOffset.UtcNow,
            ReplyTo = replyTo,
        };
        var messageRequest = new MessageRequest
        {
            MessageEnvelope = messageEnvelope,
            Receiver = receiver,
            // CancellationToken = cancellationTokenSource.Token,
        };

        _ = _requestWorker.WriteAsync(messageRequest, cancellationTokenSource.Token);
        return messageEnvelope;
    }

    public void Send(ImmutableArray<Peer> receivers, IMessage message)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        Parallel.ForEach(receivers, peer => Send(peer, message));
    }

    // public ValueTask ReplyAsync(MessageEnvelope requestEnvelope, IMessage message, bool hasNext)
    // {
    //     ObjectDisposedException.ThrowIf(IsDisposed, this);

    //     if (!IsRunning || _responseQueue is null)
    //     {
    //         throw new InvalidOperationException("Transport is not running.");
    //     }

    //     var messageResponse = new MessageResponse
    //     {
    //         MessageEnvelope = new MessageEnvelope
    //         {
    //             Identity = requestEnvelope.Identity,
    //             Message = message,
    //             Protocol = _options.Protocol,
    //             Sender = Peer,
    //             Timestamp = DateTimeOffset.UtcNow,
    //         },
    //         Receiver = requestEnvelope.Sender,
    //         // HasNext = hasNext,
    //     };
    //     _responseQueue.Enqueue(messageResponse);
    //     return ValueTask.CompletedTask;
    // }

    private void Router_ReceiveReady(object? sender, NetMQSocketEventArgs e)
    {
        try
        {
            var receivedMessage = new NetMQMessage();
            if (!e.Socket.TryReceiveMultipartMessage(TimeSpan.Zero, ref receivedMessage))
            {
                return;
            }

            var rawMessage = new NetMQMessage(receivedMessage.Skip(1));
            var messageEnvelope = NetMQMessageCodec.Decode(rawMessage);
            messageEnvelope.Validate(_options.Protocol, _options.MessageLifetime);
            // var replyContext = new NetMQReplyContext(this, messageEnvelope);
            MessageHandlers.Handle(messageEnvelope);
        }
        catch
        {
            // log
        }
    }

    private void ResponseQueue_ReceiveReady(object? sender, NetMQQueueEventArgs<MessageResponse> e)
    {
        if (e.Queue.TryDequeue(out var messageResponse, TimeSpan.Zero))
        {
            var messageEnvelope = messageResponse.MessageEnvelope;
            var rawMessage = NetMQMessageCodec.Encode(messageEnvelope, signer);
            var identity = Encoding.UTF8.GetBytes(messageResponse.Receiver.ToString());
            rawMessage.Push(identity);
            // rawMessage.Append(messageResponse.HasNext ? 1 : 0);
            if (_router.TrySendMultipartMessage(TimeSpan.FromSeconds(1), rawMessage))
            {
                // Successfully sent the message
            }
        }
        else
        {
            // Handle the case where no message was dequeued
        }
    }

    private async Task<Channel<MessageResponse>> WriteAsync(
        Peer receiver, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        if (_requestWorker is null)
        {
            throw new InvalidOperationException("Transport is not running");
        }

        using var timeoutCancellationTokenSource = new CancellationTokenSource(_options.SendTimeout);
        using var cancellationTokenSource = CreateCancellationTokenSource(
            cancellationToken, timeoutCancellationTokenSource.Token);
        var channel = Channel.CreateUnbounded<MessageResponse>();

        try
        {
            var messageRequest = new MessageRequest
            {
                MessageEnvelope = messageEnvelope,
                Receiver = receiver,
                // Channel = channel,
                // CancellationToken = cancellationTokenSource.Token,
            };
            await _requestWorker.WriteAsync(messageRequest, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException e) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            channel.Writer.TryComplete(e);
            throw new TimeoutException("The request timed out while waiting for a response.", e);
        }
        catch (ChannelClosedException e)
        {
            channel.Writer.TryComplete(e);
            throw new CommunicationException("The channel was closed before a response could be received.", e);
        }
        catch (Exception e)
        {
            channel.Writer.TryComplete(e);
            throw;
        }

        return channel;
    }

    private async Task<MessageResponse> ReadAsync(
        Channel<MessageResponse> channel, CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(_options.SendTimeout);
        using var cancellationTokenSource = CreateCancellationTokenSource(
            cancellationToken, timeoutCancellationTokenSource.Token);

        try
        {
            var response = await channel.Reader.ReadAsync(cancellationTokenSource.Token);
            var messageEnvelope = response.MessageEnvelope;
            messageEnvelope.Validate(_options.Protocol, _options.MessageLifetime);
            return response;
        }
        catch (OperationCanceledException e) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            channel.Writer.TryComplete(e);
            throw new TimeoutException("The read operation timed out while waiting for a response.", e);
        }
        catch (ChannelClosedException e)
        {
            channel.Writer.TryComplete(e);
            throw new CommunicationException("The channel was closed before a response could be received.", e);
        }
        catch (Exception e)
        {
            channel.Writer.TryComplete(e);
            throw;
        }
    }

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _requestWorker = new NetMQRequestWorker(signer);
        _poller = [_router, _responseQueue];
        _router.ReceiveReady += Router_ReceiveReady;
        _responseQueue.ReceiveReady += ResponseQueue_ReceiveReady;
        await _poller.StartAsync(cancellationToken);
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _responseQueue.ReceiveReady -= ResponseQueue_ReceiveReady;
        _router.ReceiveReady -= Router_ReceiveReady;
        if (_poller is not null)
        {
            await _poller.StopAsync(cancellationToken);
            _poller.Dispose();
            _poller = null;
        }

        if (_requestWorker is not null)
        {
            await _requestWorker.DisposeAsync();
            _requestWorker = null;
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _responseQueue.ReceiveReady -= ResponseQueue_ReceiveReady;
        _router.ReceiveReady -= Router_ReceiveReady;

        if (_poller is not null)
        {
            await _poller.DisposeAsync();
            _poller = null;
        }

        if (_requestWorker is not null)
        {
            await _requestWorker.DisposeAsync();
            _requestWorker = null;
        }

        _responseQueue.Dispose();
        _router.Dispose();
        MessageHandlers.Dispose();

        await base.DisposeAsyncCore();
    }
}
