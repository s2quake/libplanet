using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx.Synchronous;

namespace Libplanet.Net.Threading;

public class Dispatcher : IAsyncDisposable
{
    private readonly TaskFactory _factory;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly CancellationToken _cancellationToken;
    private readonly DispatcherSynchronizationContext _context;
    private readonly DispatcherScheduler _scheduler;
    private bool _isDisposed;

    public Dispatcher()
        : this(new())
    {
    }

    public Dispatcher(object owner)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;
        _scheduler = new DispatcherScheduler(_cancellationToken);
        _factory = new TaskFactory(
            _cancellationToken, TaskCreationOptions.None, TaskContinuationOptions.None, _scheduler);
        _context = new DispatcherSynchronizationContext(_factory);
        Owner = owner;
        Thread = new Thread(_scheduler.Run)
        {
            Name = $"{owner}: {owner.GetHashCode()}",
            IsBackground = true,
        };
        Thread.Start();
    }

    public event UnhandledExceptionEventHandler? UnhandledException;

    public string Name => $"{Owner}";

    public object Owner { get; }

    public Thread Thread { get; }

    public SynchronizationContext SynchronizationContext => _context;

    public override string ToString() => $"{Owner}";

    public void VerifyAccess()
    {
        if (!CheckAccess())
        {
            throw new InvalidOperationException("Thread Cannot Access");
        }
    }

    public bool CheckAccess() => Thread == Thread.CurrentThread;

    public void Post(Action action)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var task = _factory.StartNew(action, _cancellationToken);
        _ = WaitAsync(task);
    }

    public void Invoke(Action action)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (CheckAccess())
        {
            action();
            return;
        }

        var task = _factory.StartNew(action, _cancellationToken);
        try
        {
            task.Wait(_cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(e, false));
            throw;
        }
    }

    public TResult Invoke<TResult>(Func<TResult> func)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (CheckAccess())
        {
            return func();
        }

        var task = _factory.StartNew(func, _cancellationToken);
        try
        {
            task.Wait(_cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(e, false));
            throw;
        }

        return task.Result;
    }

    public Task InvokeAsync(Action action) => InvokeAsync((cancellationToken) => action(), default);

    public async Task InvokeAsync(Action<CancellationToken> action, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken);
        var task = _factory.StartNew(() => action(cancellationTokenSource.Token), cancellationTokenSource.Token);

        while (task.Status == TaskStatus.Created
            || task.Status == TaskStatus.WaitingForActivation
            || task.Status == TaskStatus.WaitingToRun)
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    "The operation was canceled before it could start.", cancellationTokenSource.Token);
            }

            await Task.Yield();
        }

        await WaitAsync(task);
    }

    public Task<TResult> InvokeAsync<TResult>(Func<TResult> funck)
        => InvokeAsync((cancellationToken) => funck(), default);

    public async Task<TResult> InvokeAsync<TResult>(
        Func<CancellationToken, TResult> func, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken);
        var task = _factory.StartNew(() => func(cancellationTokenSource.Token), cancellationTokenSource.Token);

        while (task.Status == TaskStatus.Created
            || task.Status == TaskStatus.WaitingForActivation
            || task.Status == TaskStatus.WaitingToRun)
        {
            if (cancellationTokenSource.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    "The operation was canceled before it could start.", cancellationTokenSource.Token);
            }

            await Task.Yield();
        }

        return await WaitAsync(task);
    }

    public async Task InvokeAsync(Func<Task> acitonTask)
        => await InvokeAsync((cancellationToken) => acitonTask(), default);

    public async Task InvokeAsync(Func<CancellationToken, Task> actionTask, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken);
        var taskCancellationToken = cancellationTokenSource.Token;
        var task = _factory.StartNew(
            () =>
            {
                var innerTask = Task.Run(() => actionTask(taskCancellationToken), taskCancellationToken);
                innerTask.WaitWithoutException();
                return innerTask;
            }, taskCancellationToken);

        while (task.Status == TaskStatus.Created
            || task.Status == TaskStatus.WaitingForActivation
            || task.Status == TaskStatus.WaitingToRun)
        {
            if (taskCancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    "The operation was canceled before it could start.", taskCancellationToken);
            }

            await Task.Yield();
        }

        var innerTask = await WaitAsync(task);
        await WaitAsync(innerTask);
    }

    public async Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> funcTask)
        => await InvokeAsync((cancellationToken) => funcTask(), default);

    public async Task<TResult> InvokeAsync<TResult>(
        Func<CancellationToken, Task<TResult>> funcTask, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken);
        var taskCancellationToken = cancellationTokenSource.Token;

        var task = _factory.StartNew(
            () =>
            {
                var innerTask = Task.Run(() => funcTask(taskCancellationToken));
                innerTask.WaitWithoutException();
                return innerTask;
            }, taskCancellationToken);

        while (task.Status == TaskStatus.Created
            || task.Status == TaskStatus.WaitingForActivation
            || task.Status == TaskStatus.WaitingToRun)
        {
            if (taskCancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    "The operation was canceled before it could start.", taskCancellationToken);
            }

            await Task.Yield();
        }

        var innerTask = await WaitAsync(task);
        return await WaitAsync(innerTask);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            await _cancellationTokenSource.CancelAsync();
            await _scheduler.WaitCloseAsync();
            _cancellationTokenSource.Dispose();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private async Task WaitAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(e, false));
            throw;
        }
    }

    private async Task<TResult> WaitAsync<TResult>(Task<TResult> task)
    {
        try
        {
            return await task;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs(e, false));
            throw;
        }
    }
}
