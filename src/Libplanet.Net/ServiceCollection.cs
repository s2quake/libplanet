using System.Collections;

namespace Libplanet.Net;

public class ServiceCollection<T>(IEnumerable<T> services)
    : ServiceBase, IEnumerable<T>
    where T : IService
{
    private readonly List<T> serviceList = [.. services];

    public ServiceCollection()
        : this([])
    {
    }

    public int Count => serviceList.Count;

    public bool SequentialExecution { get; init; }

    public T this[int index] => serviceList[index];

    public void Add(T service)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (State != ServiceState.None)
        {
            throw new InvalidOperationException(
                "Cannot add services after the collection has started or stopped.");
        }

        serviceList.Add(service);
    }

    public void AddRange(IEnumerable<T> services)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (State != ServiceState.None)
        {
            throw new InvalidOperationException(
                "Cannot add services after the collection has started or stopped.");
        }

        serviceList.AddRange(services);
    }

    public IEnumerator<T> GetEnumerator()
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

public sealed class ServiceCollection : ServiceCollection<IService>
{
    public ServiceCollection(IEnumerable<IService> services)
        : base(services)
    {
    }

    public ServiceCollection()
         : base()
    {
    }
}