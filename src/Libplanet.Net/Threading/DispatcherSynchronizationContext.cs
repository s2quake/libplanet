using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.Threading;

public sealed class DispatcherSynchronizationContext : SynchronizationContext
{
    private readonly TaskFactory _factory;

    internal DispatcherSynchronizationContext(TaskFactory factory) => _factory = factory;

    public override void Send(SendOrPostCallback d, object? state) => _factory.StartNew(() => d(state)).Wait();

    public override void Post(SendOrPostCallback d, object? state) => _factory.StartNew(() => d(state));
}
