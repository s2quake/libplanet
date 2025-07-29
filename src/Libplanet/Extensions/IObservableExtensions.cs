using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Extensions;

public static class IObservableExtensions
{
    public static async Task<T> WaitAsync<T>(this IObservable<T> @this)
        where T : notnull
        => await WaitAsync(@this, cancellationToken: default);

    public static async Task<T> WaitAsync<T>(this IObservable<T> @this, CancellationToken cancellationToken)
        where T : notnull
    {
        T? result = default;
        using var semaphore = new SemaphoreSlim(0, 1);
        using var _ = @this.Subscribe(e =>
        {
            semaphore.Release();
            result = e;
        });

        await semaphore.WaitAsync(cancellationToken);
        return result ?? throw new UnreachableException("No matching item found in observable.");
    }

    public static async Task<T> WaitAsync<T>(
        this IObservable<T> @this, Func<T, bool> predicate)
        where T : notnull
        => await WaitAsync(@this, predicate, cancellationToken: default);

    public static async Task<T> WaitAsync<T>(
        this IObservable<T> @this, Func<T, bool> predicate, CancellationToken cancellationToken)
        where T : notnull
    {
        T? result = default;
        using var semaphore = new SemaphoreSlim(0, 1);
        using var _ = @this.Subscribe(e =>
        {
            if (predicate(e))
            {
                semaphore.Release();
                result = e;
            }
        });

        await semaphore.WaitAsync(cancellationToken);
        return result ?? throw new UnreachableException("No matching item found in observable.");
    }
}
