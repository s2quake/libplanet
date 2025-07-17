using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Libplanet.Types;
using Libplanet.Types.Threading;
using NetMQ;

namespace Libplanet.Net.Transports;

internal sealed class NetMQRequestWorker : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Channel<MessageRequest> _requestChannel = Channel.CreateUnbounded<MessageRequest>();
    private readonly Task _processTask;
    private NetMQDealerSocket? _dealerSocket;
    private bool _disposed;

    public NetMQRequestWorker(ISigner signer)
    {
        var resetEvent = new ManualResetEvent(false);
        _processTask = Task.Factory.StartNew(
            () =>
            {
                using var runtime = new NetMQRuntime();
                _dealerSocket = new NetMQDealerSocket(signer, SynchronizationContext.Current!);
                var task = RunRequestChannelAsync(_requestChannel, _cancellationTokenSource.Token);
                resetEvent.Set();
                runtime.Run(task);
            },
            _cancellationTokenSource.Token,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        resetEvent.WaitOne();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_dealerSocket is not null)
            {
                await _dealerSocket.DisposeAsync();
                _dealerSocket = null;
            }

            await _cancellationTokenSource.CancelAsync();
            await TaskUtility.TryWait(_processTask);
            _cancellationTokenSource.Dispose();
            _disposed = true;
        }
    }

    public async Task WriteAsync(MessageRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(NetMQRequestWorker));

        await _requestChannel.Writer.WriteAsync(request, cancellationToken);
    }

    private async Task RunRequestChannelAsync(
        Channel<MessageRequest> channel, CancellationToken cancellationToken)
    {
        if (_dealerSocket is not { } dealerSocket)
        {
            throw new InvalidOperationException("DealerSocketHost is not initialized.");
        }

        var requestReader = channel.Reader;
        await foreach (var request in requestReader.ReadAllAsync(cancellationToken))
        {
            dealerSocket.Send(request);
            // _ = RequestMessageAsync(request, request.CancellationToken);
        }
    }

    // private async Task RequestMessageAsync(MessageRequest request, CancellationToken cancellationToken)
    // {
    //     if (_dealerSocket is null)
    //     {
    //         throw new InvalidOperationException("DealerSocketHost is not initialized.");
    //     }

    //     var receiver = request.Receiver;
    //     _dealerSocket.Send(receiver, request);

    //     // if (request.Channel?.Writer is { } requestWriter)
    //     // {
    //     //     try
    //     //     {
    //     //         await foreach (var response in _dealerSocket.ReceiveAsync(request.Identity, cancellationToken))
    //     //         {
    //     //             await requestWriter.WriteAsync(response, cancellationToken);
    //     //         }

    //     //         requestWriter.Complete();
    //     //     }
    //     //     catch (Exception e)
    //     //     {
    //     //         requestWriter.TryComplete(e);
    //     //         throw;
    //     //     }
    //     // }
    // }
}
