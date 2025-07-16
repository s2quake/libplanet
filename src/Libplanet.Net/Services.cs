using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

internal sealed class Services(params IService[] services) : IAsyncDisposable
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < services.Length; i++)
        {
            await services[i].StartAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < services.Length; i++)
        {
            await services[i].StopAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        for (var i = 0; i < services.Length; i++)
        {
            if (services[i] is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
        }
    }
}
