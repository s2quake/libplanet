using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.TestUtilities.Extensions;

public static class IObservableExtensions
{
    public static async Task<T> WaitForCountAsync<T>(
        this IObservable<T> @this, int count)
        where T : notnull
        => await WaitForCountAsync(@this, count, cancellationToken: default);

    public static async Task<T> WaitForCountAsync<T>(
        this IObservable<T> @this, int count, CancellationToken cancellationToken)
        where T : notnull
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0);

        var tcs = new TaskCompletionSource<T>();
        var i = 0;
        using var _1 = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        using var _2 = @this.Subscribe(e =>
        {
            if (i++ == count)
            {
                tcs.TrySetResult(e);
            }
        });

        return await tcs.Task;
    }
}
