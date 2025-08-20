namespace Libplanet.Net.Tests;

public static class IServiceExtensions
{
    public static void StartAfter(this IService @this, int millisecondsDelay)
        => StartAfter(@this, TimeSpan.FromMilliseconds(millisecondsDelay));

    public static void StartAfter(this IService @this, TimeSpan delay)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay);
            await @this.StartAsync(default);
        });
    }
}
