using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Libplanet.Types;
using Libplanet.Types.Threading;
using NetMQ;

namespace Libplanet.Net.NetMQ;

internal sealed class NetMQRequestWorker : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Channel<MessageRequest> _requestChannel = Channel.CreateUnbounded<MessageRequest>();
    private readonly Task _processTask;
    // private readonly NetMQSender _sender;
    private bool _disposed;

    public NetMQRequestWorker(ISigner signer)
    {
        // var tcs = new TaskCompletionSource<NetMQSender>();
        _processTask = Task.Factory.StartNew(
            () =>
            {
                using var runtime = new NetMQRuntime();
                using var dealerSocket = new NetMQSender(signer, SynchronizationContext.Current!);
                var task = RunRequestChannelAsync(dealerSocket, _requestChannel, _cancellationTokenSource.Token);
                // tcs.SetResult(dealerSocket);
                runtime.Run(task);
            },
            _cancellationTokenSource.Token,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        // tcs.Task.Wait();
        // _sender = tcs.Task.Result;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            // await _sender.DisposeAsync();
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

    private static async Task RunRequestChannelAsync(
        NetMQSender sender, Channel<MessageRequest> channel, CancellationToken cancellationToken)
    {
        // if (_dealerSocket is not { } dealerSocket)
        // {
        //     throw new InvalidOperationException("DealerSocketHost is not initialized.");
        // }

        var requestReader = channel.Reader;
        await foreach (var request in requestReader.ReadAllAsync(cancellationToken))
        {
            Trace.WriteLine("Send request: " + request.Identity);
            sender.Send(request);
        }
    }
}
