using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

internal sealed class ServicesCollection
    : ServiceBase, IEnumerable<IService>
{
    private readonly List<IService> serviceList = [];

    public int Count => serviceList.Count;

    public IService this[int index] => serviceList[index];

    public void Add(IService service)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (State != ServiceState.None)
        {
            throw new InvalidOperationException(
                "Cannot add services after the collection has started or stopped.");
        }

        serviceList.Add(service);
    }

    public IEnumerator<IService> GetEnumerator()
    {
        foreach (var service in serviceList)
        {
            yield return service;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < serviceList.Count; i++)
        {
            await serviceList[i].StartAsync(cancellationToken);
        }
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < serviceList.Count; i++)
        {
            await serviceList[i].StopAsync(cancellationToken);
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        for (var i = 0; i < serviceList.Count; i++)
        {
            if (serviceList[i] is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
        }

        await base.DisposeAsyncCore();
    }
}
