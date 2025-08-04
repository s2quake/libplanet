using System.Collections;

namespace Libplanet.Net;

public sealed class ServiceCollection(IEnumerable<IService> services)
    : ServiceBase, IEnumerable<IService>
{
    private readonly List<IService> serviceList = [.. services];

    public ServiceCollection()
        : this([])
    {
    }

    public int Count => serviceList.Count;

    public bool SequentialExecution { get; init; }

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

    public void AddRange(IEnumerable<IService> services)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (State != ServiceState.None)
        {
            throw new InvalidOperationException(
                "Cannot add services after the collection has started or stopped.");
        }

        serviceList.AddRange(services);
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
