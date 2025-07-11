using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Messages;
using Libplanet.Types;
using Libplanet.Types.Threading;
using NetMQ;
using NetMQ.Sockets;

namespace Libplanet.Net.Transports;

internal sealed class DealerSocketHost(ISigner signer) : IAsyncDisposable
{
    private static readonly object _lock = new();
    private readonly Subject<MessageEnvelope> _processSubject = new();
    private readonly Dictionary<Peer, DealerSocket> _socketsByPeer = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly List<Task> _taskList = [];
    private bool _disposed;

    // private NetMQDealerSocket(Peer peer) => Peer = peer;

    // public Peer Peer { get; }

    public IObservable<MessageEnvelope> Process => _processSubject;

    public void Send(Peer peer, MessageEnvelope messageEnvelope)
    {
        if (messageEnvelope.Sender.Address != signer.Address)
        {
            throw new ArgumentException(
                $"The provided private key's address {signer.Address} does not match " +
                $"the remote peer's address {messageEnvelope.Sender.Address}.",
                nameof(messageEnvelope));
        }

        var rawMessage = NetMQMessageCodec.Encode(messageEnvelope, signer);
        var dealerSocket = GetDealerSocket(peer, messageEnvelope.Sender);
        if (!dealerSocket.TrySendMultipartMessage(rawMessage))
        {
            throw new InvalidOperationException("Failed to send message to the dealer socket.");
        }
    }

    // public async Task<MessageEnvelope> ReceiveAsync(Guid identity, CancellationToken cancellationToken)
    // {
    //     Process.Subscribe()
    //     // var dealerSocket = GetDealerSocket(peer);
    //     // var receivedRawMessage = await dealerSocket.ReceiveMultipartMessageAsync(
    //     //     expectedFrameCount: 3,
    //     //     cancellationToken: cancellationToken);
    //     // return NetMQMessageCodec.Decode(receivedRawMessage);
    // }

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
            var receivedRawMessage = await dealerSocket.ReceiveMultipartMessageAsync(
                expectedFrameCount: 3,
                cancellationToken: cancellationToken);
            var messageEnvelope = NetMQMessageCodec.Decode(receivedRawMessage);
            _processSubject.OnNext(messageEnvelope);
            await Task.Yield();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _cancellationTokenSource.CancelAsync();
            await TaskUtility.TryWhenAll(_taskList);
            foreach (var socket in _socketsByPeer.Values)
            {
                socket.Dispose();
            }

            _cancellationTokenSource.Dispose();
            _disposed = true;
        }
    }
}
