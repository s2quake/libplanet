using System.Threading;
using System.Threading.Tasks;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Tests;

public sealed class ServiceBaseTest
{
    [Fact]
    public async Task Base_Test()
    {
        await using var service = new Service();
        Assert.Equal(ServiceState.None, service.State);
    }

    [Fact]
    public async Task StartAsync()
    {
        await using var service = new Service();
        await service.StartAsync(default);
        Assert.Equal(ServiceState.Started, service.State);
    }

    [Fact]
    public async Task StartAsync_Transitioning()
    {
        await using var service = new Service();
        var task1 = service.StartAsync(default);
        var task2 = service.StartAsync(default);

        await task1;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await task2);
    }

    [Fact]
    public async Task StartAsync_Cancel()
    {
        await using var service = new Service();
        using var cancellationTokenSource = new CancellationTokenSource(10);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.StartAsync(cancellationTokenSource.Token));
        Assert.Equal(ServiceState.Faluted, service.State);
    }

    [Fact]
    public async Task StartAsync_Throw_AfterDisposed()
    {
        var service = new Service();
        await service.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await service.StartAsync(default));
    }

    [Fact]
    public async Task StopAsync()
    {
        await using var service = new Service();
        await service.StartAsync(default);
        await service.StopAsync(default);
        Assert.Equal(ServiceState.None, service.State);
    }

    [Fact]
    public async Task StopAsync_Transitioning()
    {
        await using var service = new Service();
        await service.StartAsync(default);
        var task1 = service.StopAsync(default);
        var task2 = service.StopAsync(default);

        await task1;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await task2);
    }

    [Fact]
    public async Task StopAsync_Cancel()
    {
        await using var service = new Service();
        await service.StartAsync(default);
        using var cancellationTokenSource = new CancellationTokenSource(10);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.StopAsync(cancellationTokenSource.Token));
        Assert.Equal(ServiceState.Faluted, service.State);
    }

    [Fact]
    public async Task StopAsync_Throw_AfterDisposed()
    {
        var service = new Service();
        await service.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await service.StopAsync(default));
    }

    [Fact]
    public async Task RecoverAsync()
    {
        await using var service1 = new Service
        {
            StartTask = _ => throw new InvalidOperationException("Cannot start."),
        };
        await TaskUtility.TryWait(service1.StartAsync(default));
        await Assert.ThrowsAsync<NotSupportedException>(service1.RecoverAsync);
        Assert.Equal(ServiceState.Faluted, service1.State);

        await using var service2 = new Service
        {
            StartTask = _ => throw new InvalidOperationException("Cannot start."),
            RecoverTask = Task.Delay(100),
        };
        await TaskUtility.TryWait(service2.StartAsync(default));
        await service2.RecoverAsync();
        Assert.Equal(ServiceState.None, service2.State);
    }

    [Fact]
    public async Task RecoverAsync_NotFaluted()
    {
        var service = new Service();
        await Assert.ThrowsAsync<InvalidOperationException>(service.RecoverAsync);

        await service.StartAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(service.RecoverAsync);

        await service.StopAsync(default);
        await Assert.ThrowsAsync<InvalidOperationException>(service.RecoverAsync);
    }

    [Fact]
    public async Task RecoverAsync_Failed()
    {
        await using var service = new Service
        {
            StartTask = _ => throw new InvalidOperationException("Cannot start."),
            RecoverTask = Task.FromException(new InvalidOperationException("Cannot recover.")),
        };
        var e1 = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(default));
        Assert.Equal("Cannot start.", e1.Message);
        var e2 = await Assert.ThrowsAsync<InvalidOperationException>(service.RecoverAsync);
        Assert.Equal("Cannot recover.", e2.Message);
        Assert.Equal(ServiceState.Faluted, service.State);
    }

    [Fact]
    public async Task RecoverAsync_Throw_AfterDisposed()
    {
        var service = new Service();
        await service.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(service.RecoverAsync);
    }

    [Fact]
    public async Task DisposeAsync_Test()
    {
        var service1 = new Service();
        await service1.DisposeAsync();
        Assert.Equal(ServiceState.Disposed, service1.State);

        var service2 = new Service();
        await service2.StartAsync(default);
        await service2.DisposeAsync();
        Assert.Equal(ServiceState.Disposed, service2.State);

        var service3 = new Service();
        await service3.StartAsync(default);
        await service3.StopAsync(default);
        await service3.DisposeAsync();
        Assert.Equal(ServiceState.Disposed, service3.State);

        var service4 = new Service()
        {
            StartTask = _ => throw new InvalidOperationException("Cannot start."),
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service4.StartAsync(default));
        await service4.DisposeAsync();
        Assert.Equal(ServiceState.Disposed, service2.State);

        var service5 = new Service();
        await service5.DisposeAsync();
        await service5.DisposeAsync();
        Assert.Equal(ServiceState.Disposed, service2.State);
    }

    [Fact]
    public async Task DisposeAsync_Transitioning()
    {
        var service1 = new Service();
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 10),
            async (_, _) => await service1.DisposeAsync());
        Assert.Equal(ServiceState.Disposed, service1.State);

        var service2 = new Service();
        await Task.WhenAll(
            service2.StartAsync(default),
            service2.DisposeAsync().AsTask());
        Assert.Equal(ServiceState.Disposed, service2.State);

        var service3 = new Service();
        await service3.StartAsync(default);
        await Task.WhenAll(
            service3.StopAsync(default),
            service3.DisposeAsync().AsTask());
        Assert.Equal(ServiceState.Disposed, service2.State);

        var service4 = new Service
        {
            StartTask = _ => throw new InvalidOperationException("Cannot start."),
            RecoverTask = Task.Delay(100),
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service4.StartAsync(default));
        await Task.WhenAll(
            service4.RecoverAsync(),
            service4.DisposeAsync().AsTask());
        Assert.Equal(ServiceState.Disposed, service4.State);

        var service5 = new Service();
#pragma warning disable S5034 // "ValueTask" should be consumed correctly
        await Task.WhenAll(
            service5.DisposeAsync().AsTask(),
            service5.DisposeAsync().AsTask());
#pragma warning restore S5034 // "ValueTask" should be consumed correctly
        await service5.DisposeAsync();
        Assert.Equal(ServiceState.Disposed, service5.State);
    }

    [Fact]
    public async Task CreateCancellationTokenSource()
    {
        await using var service1 = new Service();
        await service1.StartAsync(default);
        var task1 = service1.DelayAsync(TimeSpan.FromSeconds(10), default);
        await service1.StopAsync(default);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task1);

        var service2 = new Service();
        await service2.StartAsync(default);
        var task2 = service2.DelayAsync(TimeSpan.FromSeconds(10), default);
        await service2.DisposeAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task2);
    }

    [Fact]
    public async Task CreateCancellationTokenSource_Throw_WhenStateIsNotStarted()
    {
        await using var service = new Service();
        var e = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.DelayAsync(TimeSpan.FromSeconds(10), default));
        Assert.StartsWith("Cannot create a cancellation token source", e.Message);
    }

    [Fact]
    public async Task CreateCancellationTokenSource_Throw_WhenDisposed()
    {
        var service = new Service();
        await service.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await service.DelayAsync(TimeSpan.FromSeconds(10), default));
    }

    private sealed class Service : ServiceBase
    {
        public Func<CancellationToken, Task> StartTask { get; init; } = _ => Task.Delay(100);

        public Func<CancellationToken, Task> StopTask { get; init; } = _ => Task.Delay(100);

        public Task? RecoverTask { get; init; }

        public Task DisposeTask { get; init; } = Task.Delay(100);

        public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            using var cancellationTokenSource = CreateCancellationTokenSource(cancellationToken);
            await TaskUtility.TryDelay(delay, cancellationTokenSource.Token);
            cancellationTokenSource.Token.ThrowIfCancellationRequested();
        }

        protected override Task OnRecoverAsync() => RecoverTask ?? base.OnRecoverAsync();

        protected override async Task OnStartAsync(CancellationToken cancellationToken)
            => await StartTask(cancellationToken);

        protected override async Task OnStopAsync(CancellationToken cancellationToken)
            => await StopTask(cancellationToken);

        protected override async ValueTask DisposeAsyncCore() => await DisposeTask;
    }
}
