namespace Libplanet.Net;

public static class IServiceExtensions
{
    public static async Task RestartAsync(this IService @this, CancellationToken cancellationToken)
    {
        await @this.StopAsync(cancellationToken);
        await @this.StartAsync(cancellationToken);
    }

    public static Task StartAsync(this IService @this) => @this.StartAsync(default);

    public static Task StopAsync(this IService @this) => @this.StopAsync(default);
}
