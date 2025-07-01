using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Extensions;

public static class IObservableExtensions
{
    public static async Task<T> WaitAsync<T>(this IObservable<T> @this, CancellationToken cancellationToken)
        where T : notnull
    {
        T? result = default;
        using var resetEvent = new ManualResetEvent(false);
        using var _ = @this.Subscribe(e =>
        {
            resetEvent.Set();
            result = e;
        });

        await Task.Run(resetEvent.WaitOne, cancellationToken);
        return result ?? throw new UnreachableException("No matching item found in observable.");
    }

    public static async Task<T> WaitAsync<T>(
        this IObservable<T> @this, Func<T, bool> predicate, CancellationToken cancellationToken)
        where T : notnull
    {
        T? result = default;
        using var resetEvent = new ManualResetEvent(false);
        using var _ = @this.Subscribe(e =>
        {
            if (predicate(e))
            {
                resetEvent.Set();
                result = e;
            }
        });

        await Task.Run(resetEvent.WaitOne, cancellationToken);
        return result ?? throw new UnreachableException("No matching item found in observable.");
    }
}
