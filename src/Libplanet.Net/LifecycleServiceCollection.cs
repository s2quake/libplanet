using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net;

public sealed class LifecycleServiceCollection
    : LifecycleServiceBase, IEnumerable<ILifecycleService>
{
    private readonly List<ILifecycleService> serviceList = [];

    public int Count => serviceList.Count;

    public bool SequentialExecution { get; init; }

    public ILifecycleService this[int index] => serviceList[index];

    public void Add(ILifecycleService service)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (State != LifecycleServiceState.None)
        {
            throw new InvalidOperationException(
                "Cannot add services after the collection has started or stopped.");
        }

        serviceList.Add(service);
    }

    public IEnumerator<ILifecycleService> GetEnumerator()
    {
        foreach (var service in serviceList)
        {
            yield return service;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        if (SequentialExecution)
        {
            for (var i = 0; i < serviceList.Count; i++)
            {
                await serviceList[i].StartAsync(cancellationToken);
            }
        }
        else
        {
            var tasks = serviceList.Select(service => service.StartAsync(cancellationToken)).ToArray();
            await Task.WhenAll(tasks);
        }
    }

    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        if (SequentialExecution)
        {
            for (var i = 0; i < serviceList.Count; i++)
            {
                await serviceList[i].StopAsync(cancellationToken);
            }
        }
        else
        {
            var tasks = serviceList.Select(service => service.StopAsync(cancellationToken)).ToArray();
            await Task.WhenAll(tasks);
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        if (SequentialExecution)
        {
            for (var i = 0; i < serviceList.Count; i++)
            {
                if (serviceList[i] is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
            }
        }
        else
        {
            await Parallel.ForEachAsync(serviceList, async (service, _) =>
            {
                if (service is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
            });
        }

        await base.DisposeAsyncCore();
    }
}
