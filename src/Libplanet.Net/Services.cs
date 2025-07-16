using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

internal sealed class ServicesCollection(params IService[] services)
    : IEnumerable<IService>, IAsyncDisposable
{
    private readonly ImmutableArray<IService> services = [.. services];

    public int Count => services.Length;

    public IService this[int index] => services[index];

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

    public IEnumerator<IService> GetEnumerator()
    {
        foreach(var service in services)
        {
            yield return service;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
