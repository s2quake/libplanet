using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Net.Threading;
using Libplanet.Types;
using Libplanet.Types.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace Libplanet.Net.Transports;

internal sealed class NetMQDealerSocket(ISigner signer, Peer peer) : IAsyncDisposable
{
    private static readonly object _lock = new();
    private readonly SynchronizationContext _synchronizationContext
        = SynchronizationContext.Current ?? throw new InvalidOperationException(
            "SynchronizationContext.Current is null. " +
            "Ensure that this code is running in a context that supports synchronization contexts.");
    private readonly Subject<MessageResponse> _processSubject = new();
    private readonly Dictionary<Peer, DealerSocket> _socketsByPeer = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<Task> _taskList = [];
    private bool _disposed;

    public IObservable<MessageResponse> Process => _processSubject;

    public void Send(Peer peer, MessageRequest request)
    {
        if (_synchronizationContext != SynchronizationContext.Current)
        {
            throw new InvalidOperationException(
                "Send method must be called from the same synchronization context as the one used to create this instance.");
        }

        if (request.Sender.Address != signer.Address)
        {
            throw new ArgumentException(
                $"The provided private key's address {signer.Address} does not match " +
                $"the remote peer's address {request.Sender.Address}.",
                nameof(request));
        }

        var messageEnvelope = request.MessageEnvelope;
        var rawMessage = NetMQMessageCodec.Encode(messageEnvelope, signer);
        var dealerSocket = GetDealerSocket(peer, request.Sender);
        request.CancellationToken.ThrowIfCancellationRequested();
        if (!dealerSocket.TrySendMultipartMessage(rawMessage))
        {
            throw new InvalidOperationException("Failed to send message to the dealer socket.");
        }
    }

    public async IAsyncEnumerable<MessageResponse> ReceiveAsync(
        Guid identity, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<MessageResponse>();
        using var _ = _processSubject.Subscribe(response =>
        {
            if (response.Identity == identity)
            {
                channel.Writer.TryWrite(response);

            }
        });

        await foreach (var response in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return response;
            if (!response.HasNext)
            {
                break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            Trace.WriteLine("dealer socket disposing.");
            await _cancellationTokenSource.CancelAsync();
            await TaskUtility.TryWhenAll(_taskList);
            await _synchronizationContext.PostAsync(() =>
            {
                Trace.WriteLine("dealer socket items disposing.");
                foreach (var socket in _socketsByPeer.Values)
                {
                    socket.Dispose();
                }
                Trace.WriteLine("dealer socket items disposed.");
            }, default);

            _cancellationTokenSource.Dispose();
            Trace.WriteLine("dealer socket disposed.");
            _disposed = true;
        }
    }

    public DealerSocket GetDealerSocket(Peer peer, Peer sender)
    {
        lock (_lock)
        {
            if (!_socketsByPeer.TryGetValue(peer, out var socket))
            {
                var address = GetAddress(peer);
                socket = new DealerSocket();
                socket.Options.DisableTimeWait = true;
                socket.Options.Identity = Encoding.UTF8.GetBytes(sender.ToString());
                Trace.WriteLine($"dealer 1: {sender}");
                Trace.WriteLine($"dealer 2: {ByteUtility.Hex(socket.Options.Identity)}");
                socket.Connect(address);
                _socketsByPeer[peer] = socket;
                _taskList.Add(RunAsync(socket, _cancellationTokenSource.Token));
            }

            return socket;
        }
    }

    private static string GetAddress(Peer peer)
    {
        var host = peer.EndPoint.Host;
        var port = peer.EndPoint.Port;
        var addresses = Dns.GetHostAddresses(host);
        var ipv4 = addresses.FirstOrDefault(addr => addr.AddressFamily is AddressFamily.InterNetwork)
            ?? throw new InvalidOperationException($"Failed to resolve for {host}");

        return $"tcp://{ipv4}:{port}";
    }

    private async Task RunAsync(DealerSocket dealerSocket, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var rawMessage = await dealerSocket.ReceiveMultipartMessageAsync(
                expectedFrameCount: 3,
                cancellationToken: cancellationToken);
            var messageEnvelope = NetMQMessageCodec.Decode(rawMessage);
            var hasNext = rawMessage.Last.ConvertToInt32() == 1;
            var messageResponse = new MessageResponse
            {
                MessageEnvelope = messageEnvelope,
                Receiver = peer,
                HasNext = hasNext,
            };
            _processSubject.OnNext(messageResponse);
            await Task.Yield();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
