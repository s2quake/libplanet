namespace Libplanet.Net.Tests;

public static class IServiceExtensions
{
    public static void StartAfter(this IService service, int millisecondsDelay)
        => StartAfter(service, TimeSpan.FromMilliseconds(millisecondsDelay));

    public static void StartAfter(this IService service, TimeSpan delay)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay);
            await service.StartAsync(default);
        });
    }
}
