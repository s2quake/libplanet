namespace Libplanet.Net.Threading;

public static class DispatcherExtensions
{
    public static Task PostAfterAsync(this Dispatcher @this, Action aciton, TimeSpan delay)
        => PostAfterAsync(@this, _ => aciton(), delay, default);

    public static async Task PostAfterAsync(
        this Dispatcher @this, Action<CancellationToken> aciton, TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "Delay cannot be negative.");
        }

        await Task.Delay(delay, cancellationToken);
        await @this.InvokeAsync(aciton, cancellationToken);
    }

    public static Task PostAfterAsync(this Dispatcher @this, Action aciton, int millisecondsDelay)
        => PostAfterAsync(@this, _ => aciton(), TimeSpan.FromMilliseconds(millisecondsDelay), default);

    public static Task PostAfterAsync(
        this Dispatcher @this,
        Action<CancellationToken> aciton,
        int millisecondsDelay,
        CancellationToken cancellationToken)
        => PostAfterAsync(@this, aciton, TimeSpan.FromMilliseconds(millisecondsDelay), cancellationToken);
}
