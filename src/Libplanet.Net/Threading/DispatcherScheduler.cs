using System.Collections.Concurrent;

namespace Libplanet.Net.Threading;

public sealed class DispatcherScheduler : TaskScheduler
{
    private readonly CancellationToken _cancellationToken;
    private readonly ConcurrentQueue<Task> _taskQueue = [];
    private readonly ManualResetEvent _executionEventSet = new(false);
    private bool _isRunning = true;
    private bool _isClosed;

    internal DispatcherScheduler(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    internal async Task WaitCloseAsync()
    {
        if (_isClosed)
        {
            throw new InvalidOperationException("Dispatcher is already closed.");
        }

        while (_isRunning)
        {
            try
            {
                _executionEventSet.Set();
            }
            catch
            {
                _executionEventSet.Close();
            }

            await Task.Yield();
        }

        _executionEventSet.Dispose();
        _isClosed = true;
    }

    internal void Run()
    {
        try
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                if (_taskQueue.TryDequeue(out var task))
                {
                    TryExecuteTask(task);
                }
                else
                {
                    try
                    {
                        _executionEventSet.Reset();
                        _executionEventSet.WaitOne(1000);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                }
            }

            if (_isClosed)
            {
                throw new InvalidOperationException("Dispatcher is already closed.");
            }
        }
        finally
        {
            _isRunning = false;
        }
    }

    protected override IEnumerable<Task> GetScheduledTasks() => _taskQueue;

    protected override void QueueTask(Task task)
    {
        if (!_cancellationToken.IsCancellationRequested)
        {
            _taskQueue.Enqueue(task);
            _executionEventSet.Set();
        }
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        return false;
    }
}
