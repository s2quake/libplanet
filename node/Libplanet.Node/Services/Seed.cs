using System.Net;
using Libplanet.Net;
using Libplanet.Net.NetMQ;
using Libplanet.Node.Options;
using Libplanet.Types;
using Serilog;

namespace Libplanet.Node.Services;

internal class Seed(SeedOptions seedOptions) : IAsyncDisposable
{
    private readonly ILogger _logger = Log.ForContext<Seed>();

    private ITransport? _transport;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task _task = Task.CompletedTask;
    private Task _refreshTask = Task.CompletedTask;

    public event EventHandler<SeedMessageEventArgs>? MessageReceived;

    public ILogger Logger => _logger;

    public bool IsRunning { get; private set; }

    public PeerCollection Peers { get; } = new(seedOptions);

    public Net.Peer BoundPeer => new Net.Peer
    {
        Address = PrivateKey.Parse(seedOptions.PrivateKey).Address,
        EndPoint = (DnsEndPoint)EndPointUtility.Parse(seedOptions.EndPoint),
    };

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Seed node is already running.");
        }

        _cancellationTokenSource
            = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _transport = CreateTransport();
        // _transport.Process.Subscribe(ReceiveMessageAsync);
        await _transport.StartAsync(_cancellationTokenSource.Token);
        _refreshTask = RefreshContinuouslyAsync(_cancellationTokenSource.Token);
        IsRunning = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException("Seed node is not running.");
        }

        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        if (_transport is not null)
        {
            await _transport.StopAsync(cancellationToken);
            await _transport.DisposeAsync();
            _transport = null;
        }

        await _refreshTask;
        await _task;
        _refreshTask = Task.CompletedTask;
        _task = Task.CompletedTask;
        IsRunning = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        if (_transport is not null)
        {
            await _transport.DisposeAsync();
            _transport = null;
        }

        await _refreshTask;
        await _task;
        _refreshTask = Task.CompletedTask;
        _task = Task.CompletedTask;
    }

    private NetMQTransport CreateTransport()
    {
        var privateKey = PrivateKey.Parse(seedOptions.PrivateKey);
        var protocol = Protocol.Empty;
        var endPoint = (DnsEndPoint)EndPointUtility.Parse(seedOptions.EndPoint);
        var options = new TransportOptions
        {
            Protocol = protocol,
            Host = endPoint.Host,
            Port = endPoint.Port,
        };
        return new(privateKey.AsSigner(), options);
    }

    private async Task RefreshContinuouslyAsync(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(seedOptions.RefreshInterval);
        var peers = Peers;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, cancellationToken);
                await peers.RefreshAsync(cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    // private void ReceiveMessageAsync(IReplyContext replyContext)
    // {
    //     if (_transport is null || _cancellationTokenSource is null)
    //     {
    //         throw new InvalidOperationException("Seed node is not running.");
    //     }

    //     // var messageIdentity = message.Identity;
    //     var transport = _transport;
    //     var peers = Peers;

    //     switch (replyContext.Message)
    //     {
    //         case GetPeerMessage:
    //             var alivePeers = peers.Where(item => item.IsAlive)
    //                                   .Select(item => item.BoundPeer)
    //                                   .ToArray();
    //             var neighborsMsg = new PeerMessage { Peers = [.. alivePeers] };
    //             replyContext.NextAsync(neighborsMsg);
    //             break;

    //         default:
    //             var pongMsg = new PongMessage();
    //             replyContext.NextAsync(pongMsg);
    //             break;
    //     }

    //     if (replyContext.Sender is Net.Peer boundPeer)
    //     {
    //         peers.AddOrUpdate(boundPeer, transport);
    //     }

    //     MessageReceived?.Invoke(this, new SeedMessageEventArgs(replyContext));
    // }
}
