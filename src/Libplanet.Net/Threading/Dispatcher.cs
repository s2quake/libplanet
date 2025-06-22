using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.Threading;

public class Dispatcher : IAsyncDisposable
{
    private readonly TaskFactory _factory;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly CancellationToken _cancellationToken;
    private readonly DispatcherSynchronizationContext _context;
    private readonly DispatcherScheduler _scheduler;
#if DEBUG
    private readonly System.Diagnostics.StackTrace _stackTrace;
#endif
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
#if DEBUG
        _stackTrace = new System.Diagnostics.StackTrace(true);
#endif
        Thread = new Thread(_scheduler.Run)
        {
            Name = $"{owner}: {owner.GetHashCode()}",
            IsBackground = true,
        };
        Thread.Start();
    }

    public string Name => $"{Owner}";

    public object Owner { get; }

    public Thread Thread { get; }

    public SynchronizationContext SynchronizationContext => _context;

#if DEBUG
    internal string StackTrace => $"{_stackTrace}";
#endif

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

        _factory.StartNew(action, _cancellationToken);
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
        task.Wait(_cancellationToken);
    }

    public TResult Invoke<TResult>(Func<TResult> func)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (CheckAccess())
        {
            return func();
        }

        var task = _factory.StartNew(func, _cancellationToken);
        task.Wait(_cancellationToken);
        return task.Result;
    }

    public async Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> callback)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var task = callback();
        task.Start(_scheduler);
        await task;
        return task.Result;
    }

    public async Task<TResult> InvokeAsync<TResult>(Func<CancellationToken, Task<TResult>> callback)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var task = callback(_cancellationToken);
        task.Start(_scheduler);
        await task;
        return task.Result;
    }

    public Task InvokeAsync(Action action) => InvokeAsync(action, default);

    public async Task InvokeAsync(Action action, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken);
        await _factory.StartNew(action, cancellationTokenSource.Token);
    }

    public Task<TResult> InvokeAsync<TResult>(Func<TResult> callback) => InvokeAsync(callback, default);

    public async Task<TResult> InvokeAsync<TResult>(Func<TResult> callback, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken, cancellationToken);
        return await _factory.StartNew(callback, cancellationTokenSource.Token);
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
}
