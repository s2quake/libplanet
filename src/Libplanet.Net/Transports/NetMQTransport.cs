using System.Diagnostics;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Options;
using Libplanet.Types;
using Libplanet.Types.Threading;
using NetMQ;

namespace Libplanet.Net.Transports;

public sealed class NetMQTransport(ISigner signer, TransportOptions options)
    : ITransport
{
    private readonly Subject<IReplyContext> _processSubject = new();
    private readonly NetMQRouterSocket _router = new(signer.Address, options.Host, options.Port);
    private readonly NetMQQueue<MessageResponse> _replyQueue = new();
    private readonly TransportOptions _options = ValidationUtility.ValidateAndReturn(options);

    private Channel<MessageRequest>? _requestChannel;
    private NetMQPoller? _poller;
    private NetMQDealerSocket? _dealerSocket;
    private CancellationTokenSource? _cancellationTokenSource = new();
    private CancellationToken _cancellationToken;
    private Task _processTask = Task.CompletedTask;
    private bool _disposed;

    public NetMQTransport(ISigner signer)
        : this(signer, new TransportOptions())
    {
    }

    public IObservable<IReplyContext> Process => _processSubject;

    public Peer Peer => _router.Peer;

    public bool IsRunning { get; private set; }

    public Protocol Protocol => _options.Protocol;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
        {
            throw new InvalidOperationException("Transport is already running.");
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;
        _requestChannel = Channel.CreateUnbounded<MessageRequest>();
        _poller = [_router, _replyQueue];
        Trace.WriteLine($"NetMQPoller({_poller.GetHashCode()}) starting...");
        _router.ReceiveReady += Router_ReceiveReady;
        _replyQueue.ReceiveReady += ReplyQueue_ReceiveReady;

        var e = new ManualResetEvent(false);
        _processTask = Task.Factory.StartNew(
            () =>
            {
                using var runtime = new NetMQRuntime();
                var task = RunRequestChannelAsync(_requestChannel, _cancellationToken);
                _dealerSocket = new NetMQDealerSocket(signer, Peer);
                Trace.WriteLine($"SynchronizationContext({SynchronizationContext.Current?.GetHashCode()})");
                e.Set();
                runtime.Run(task);
                Trace.WriteLine($"NetMQRuntime({runtime.GetHashCode()}) stop. {_cancellationToken.IsCancellationRequested}");
            },
            _cancellationToken,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        e.WaitOne();
        await _poller.StartAsync(cancellationToken);


        IsRunning = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        _replyQueue.ReceiveReady -= ReplyQueue_ReceiveReady;
        _router.ReceiveReady -= Router_ReceiveReady;
        if (_poller is not null)
        {
            await _poller.StopAsync(cancellationToken);
            _poller = null;
        }

        if (_dealerSocket is not null)
        {
            await _dealerSocket.DisposeAsync();
            _dealerSocket = null;
        }

        _requestChannel?.Writer.TryComplete();
        _requestChannel = null;

        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
        }

        await TaskUtility.TryWait(_processTask);
        _processTask = Task.CompletedTask;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        IsRunning = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _replyQueue.ReceiveReady -= ReplyQueue_ReceiveReady;
            _router.ReceiveReady -= Router_ReceiveReady;

            if (_poller is not null)
            {
                await _poller.DisposeAsync();
                _poller = null;
            }

            if (_dealerSocket is not null)
            {
                await _dealerSocket.DisposeAsync();
            }

            Trace.WriteLine("123123123");
            _requestChannel?.Writer.TryComplete();
            _requestChannel = null;
            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync();
            }

            await TaskUtility.TryWait(_processTask);

            _processTask = Task.CompletedTask;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _replyQueue.Dispose();
            _router.Dispose();
            _processSubject.Dispose();
            _disposed = true;
        }
    }

    public async IAsyncEnumerable<IMessage> SendAsync(
        Peer receiver, IMessage message, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        var messageEnvelope = new MessageEnvelope
        {
            Identity = Guid.NewGuid(),
            Message = message,
            Protocol = _options.Protocol,
            Sender = Peer,
            Timestamp = DateTimeOffset.UtcNow,
        };
        var channel = await WriteAsync(receiver, messageEnvelope, cancellationToken);
        var hasNext = true;
        while (hasNext)
        {
            var response = await ReadAsync(channel, cancellationToken);
            Trace.WriteLine("sendasync: 2");
            hasNext = response.HasNext;
            yield return response.MessageEnvelope.Message;
        }

        Trace.WriteLine("sendasync: 3");
        channel.Writer.TryComplete();
        Trace.WriteLine("sendasync: 4");
    }

    public void Broadcast(ImmutableArray<Peer> peers, IMessage message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        Parallel.ForEach(peers, peer => Post(peer, message));
    }

    public void Reply(MessageEnvelope requestEnvelope, IMessage message, bool hasNext)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsRunning || _replyQueue is null)
        {
            throw new InvalidOperationException("Transport is not running.");
        }

        var messageResponse = new MessageResponse
        {
            MessageEnvelope = new MessageEnvelope
            {
                Identity = requestEnvelope.Identity,
                Message = message,
                Protocol = _options.Protocol,
                Sender = Peer,
                Timestamp = DateTimeOffset.UtcNow,
            },
            Receiver = requestEnvelope.Sender,
            HasNext = hasNext,
        };
        _replyQueue.Enqueue(messageResponse);
    }

    private void Router_ReceiveReady(object? sender, NetMQSocketEventArgs e)
    {
        try
        {
            var receivedMessage = new NetMQMessage();
            if (!e.Socket.TryReceiveMultipartMessage(TimeSpan.Zero, ref receivedMessage))
            {
                return;
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var rawMessage = new NetMQMessage(receivedMessage.Skip(1));
            var messageEnvelope = NetMQMessageCodec.Decode(rawMessage);
            messageEnvelope.Validate(_options.Protocol, _options.MessageLifetime);
            Trace.WriteLine($"Received {messageEnvelope.Message.GetType().Name}: {messageEnvelope.Sender}");
            var replyContext = new NetMQReplyContext(this, messageEnvelope);
            _processSubject.OnNext(replyContext);
        }
        catch
        {
            // log
        }
    }

    private void ReplyQueue_ReceiveReady(object? sender, NetMQQueueEventArgs<MessageResponse> e)
    {
        if (e.Queue.TryDequeue(out var messageResponse, TimeSpan.Zero))
        {
            var messageEnvelope = messageResponse.MessageEnvelope;
            var rawMessage = NetMQMessageCodec.Encode(messageEnvelope, signer);
            var identity = Encoding.UTF8.GetBytes(messageResponse.Receiver.ToString());
            Trace.WriteLine($"reply 1: {messageResponse.Receiver}");
            Trace.WriteLine($"reply 2: {ByteUtility.Hex(identity)}");
            rawMessage.Push(identity);
            rawMessage.Append(messageResponse.HasNext ? 1 : 0);
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

    private async Task RunRequestChannelAsync(
        Channel<MessageRequest> channel, CancellationToken cancellationToken)
    {
        Trace.WriteLine("RunRequestChannelAsync begin.");
        var requestReader = channel.Reader;
        await foreach (var request in requestReader.ReadAllAsync(cancellationToken))
        {
            _ = RequestMessageAsync(request, request.CancellationToken);
        }
        Trace.WriteLine("RunRequestChannelAsync end.");
    }

    private async Task RequestMessageAsync(MessageRequest request, CancellationToken cancellationToken)
    {
        if (_dealerSocket is null)
        {
            throw new InvalidOperationException("DealerSocketHost is not initialized.");
        }

        var receiver = request.Receiver;
        _dealerSocket.Send(receiver, request);

        if (request.Channel?.Writer is { } requestWriter)
        {
            try
            {
                await foreach (var response in _dealerSocket.ReceiveAsync(request.Identity, cancellationToken))
                {
                    await requestWriter.WriteAsync(response, cancellationToken);
                }

                requestWriter.Complete();
                Trace.WriteLine("Received 12345: ");
            }
            catch (Exception e)
            {
                requestWriter.TryComplete(e);
            }
        }

        Trace.WriteLine("Received 123456: ");
    }

    private async Task<Channel<MessageResponse>> WriteAsync(
        Peer receiver, MessageEnvelope messageEnvelope, CancellationToken cancellationToken)
    {
        if (_requestChannel is null)
        {
            throw new InvalidOperationException("Transport is not running");
        }

        using var timeoutCancellationTokenSource = new CancellationTokenSource(_options.SendTimeout);
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken, timeoutCancellationTokenSource.Token);
        var channel = Channel.CreateUnbounded<MessageResponse>();

        try
        {
            var messageRequest = new MessageRequest
            {
                MessageEnvelope = messageEnvelope,
                Receiver = receiver,
                Channel = channel,
                CancellationToken = cancellationTokenSource.Token,
            };
            await _requestChannel.Writer.WriteAsync(messageRequest, cancellationTokenSource.Token);
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
        using var timeoutCancellationTokenSource = new CancellationTokenSource(_options.ReceiveTimeout);
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken, timeoutCancellationTokenSource.Token);

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

    private void Post(Peer receiver, IMessage message)
    {
        if (_requestChannel is null)
        {
            throw new InvalidOperationException("Transport is not running");
        }

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationToken);

        var messageEnvelope = new MessageEnvelope
        {
            Identity = Guid.NewGuid(),
            Message = message,
            Protocol = _options.Protocol,
            Sender = Peer,
            Timestamp = DateTimeOffset.UtcNow,
        };
        var messageRequest = new MessageRequest
        {
            MessageEnvelope = messageEnvelope,
            Receiver = receiver,
            CancellationToken = cancellationTokenSource.Token,
        };

        _ = _requestChannel.Writer.WriteAsync(messageRequest, cancellationTokenSource.Token).AsTask();
    }
}
