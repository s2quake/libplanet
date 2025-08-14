using System.Reactive.Linq;

namespace Libplanet.Extensions;

public static class IObservableExtensions
{
    public static async Task<T> WaitAsync<T>(this IObservable<T> @this)
        where T : notnull
        => await WaitAsync(@this, cancellationToken: default);

    public static async Task<T> WaitAsync<T>(this IObservable<T> @this, CancellationToken cancellationToken)
        where T : notnull
    {
        var tcs = new TaskCompletionSource<T>();
        using var _1 = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        using var _2 = @this.Subscribe(e =>
        {
            tcs.TrySetResult(e);
        });

        return await tcs.Task;
    }

    public static async Task<T> WaitAsync<T>(
        this IObservable<T> @this, Func<T, bool> predicate)
        where T : notnull
        => await WaitAsync(@this, predicate, cancellationToken: default);

    public static async Task<T> WaitAsync<T>(
        this IObservable<T> @this, Func<T, bool> predicate, CancellationToken cancellationToken)
        where T : notnull
    {
        var tcs = new TaskCompletionSource<T>();
        using var _1 = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        using var _2 = @this.Subscribe(e =>
        {
            if (predicate(e))
            {
                tcs.TrySetResult(e);
            }
        });

        return await tcs.Task;
    }
}
