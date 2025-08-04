namespace Libplanet.Net;

public static class IServiceExtensions
{
    public static async Task RestartAsync(this IService @this, CancellationToken cancellationToken)
    {
        await @this.StopAsync(cancellationToken);
        await @this.StartAsync(cancellationToken);
    }
}
