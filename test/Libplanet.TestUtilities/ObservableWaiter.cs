using Libplanet.Extensions;

namespace Libplanet.TestUtilities;

public struct ObservableWaiter<T>(
    IObservable<T> observable, Func<T, bool> predicate, CancellationToken cancellationToken)
    : IAsyncDisposable
    where T : notnull
{
    public ObservableWaiter(IObservable<T> observable, CancellationToken cancellationToken)
        : this(observable, _ => true, cancellationToken)
    {
    }

    private readonly Task<T> _task = observable.WaitAsync(predicate, cancellationToken);

    public TimeSpan WaitTimeout { get; init; } = TimeSpan.FromSeconds(5);

    [AssertionMethod]
    public async readonly ValueTask DisposeAsync()
    {
        await _task.WaitAsync(WaitTimeout, cancellationToken);
    }
}
