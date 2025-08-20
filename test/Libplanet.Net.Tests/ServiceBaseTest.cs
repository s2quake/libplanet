using Libplanet.TestUtilities.Logging;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Tests;

public sealed class ServiceBaseTest(ITestOutputHelper output)
{
    [Fact]
    public async Task Base_Test()
    {
        await using var service = new Service(output);
        Assert.Equal(ServiceState.None, service.State);
    }

    [Fact]
    public async Task StartAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var service = new Service(output);
        await service.StartAsync(cancellationToken);
        Assert.Equal(ServiceState.Started, service.State);
    }

    [Fact]
    public async Task StartAsync_Transitioning()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var service = new Service(output);
        var task1 = service.StartAsync(cancellationToken);
        var task2 = service.StartAsync(cancellationToken);

        await task1;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await task2);
    }

    [Fact]
    public async Task StartAsync_Cancel()
    {
        await using var service = new Service(output);
        using var cancellationTokenSource = new CancellationTokenSource(10);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.StartAsync(cancellationTokenSource.Token));
        Assert.Equal(ServiceState.Faulted, service.State);
    }

    [Fact]
    public async Task StartAsync_Throw_AfterDisposed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = new Service(output);
        await service.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.StartAsync(cancellationToken));
    }

    [Fact]
    public async Task StopAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var service = new Service(output);
        await service.StartAsync(cancellationToken);
        await service.StopAsync(cancellationToken);
        Assert.Equal(ServiceState.None, service.State);
    }

    [Fact]
    public async Task StopAsync_Transitioning()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var service = new Service(output);
        await service.StartAsync(cancellationToken);
        var task1 = service.StopAsync(cancellationToken);
        var task2 = service.StopAsync(cancellationToken);

        await task1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => task2);
    }

    [Fact]
    public async Task StopAsync_Cancel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var service = new Service(output);
        await service.StartAsync(cancellationToken);
        using var cancellationTokenSource = new CancellationTokenSource(10);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.StopAsync(cancellationTokenSource.Token));
        Assert.Equal(ServiceState.Faulted, service.State);
    }

    [Fact]
    public async Task StopAsync_Throw_AfterDisposed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = new Service(output);
        await service.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.StopAsync(cancellationToken));
    }

    [Fact]
    public async Task RecoverAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var service1 = new Service(output)
        {
            StartTask = _ => throw new InvalidOperationException("Cannot start."),
        };
        await TaskUtility.TryWait(service1.StartAsync(cancellationToken));
        await Assert.ThrowsAsync<NotSupportedException>(service1.RecoverAsync);
        Assert.Equal(ServiceState.Faulted, service1.State);

        await using var service2 = new Service(output)
        {
            StartTask = _ => throw new InvalidOperationException("Cannot start."),
            RecoverTask = Task.Delay(100, cancellationToken),
        };
        await TaskUtility.TryWait(service2.StartAsync(cancellationToken));
        await service2.RecoverAsync();
        Assert.Equal(ServiceState.None, service2.State);
    }

    [Fact]
    public async Task RecoverAsync_NotFaluted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = new Service(output);
        await Assert.ThrowsAsync<InvalidOperationException>(service.RecoverAsync);

        await service.StartAsync(cancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(service.RecoverAsync);

        await service.StopAsync(cancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(service.RecoverAsync);
    }

    [Fact]
    public async Task RecoverAsync_Failed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var service = new Service(output)
        {
            StartTask = _ => throw new InvalidOperationException("Cannot start."),
            RecoverTask = Task.FromException(new InvalidOperationException("Cannot recover.")),
        };
        var e1 = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(cancellationToken));
        Assert.Equal("Cannot start.", e1.Message);
        var e2 = await Assert.ThrowsAsync<InvalidOperationException>(service.RecoverAsync);
        Assert.Equal("Cannot recover.", e2.Message);
        Assert.Equal(ServiceState.Faulted, service.State);
    }

    [Fact]
    public async Task RecoverAsync_Throw_AfterDisposed()
    {
        var service = new Service(output);
        await service.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(service.RecoverAsync);
    }

    [Fact]
    public async Task DisposeAsync_Test()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service1 = new Service(output);
        await service1.DisposeAsync();
        Assert.Equal(ServiceState.Disposed, service1.State);

        var service2 = new Service(output);
        await service2.StartAsync(cancellationToken);
        await service2.DisposeAsync();
        Assert.Equal(ServiceState.Disposed, service2.State);

        var service3 = new Service(output);
        await service3.StartAsync(cancellationToken);
        await service3.StopAsync(cancellationToken);
        await service3.DisposeAsync();
        Assert.Equal(ServiceState.Disposed, service3.State);

        var service4 = new Service(output)
        {
            StartTask = _ => throw new InvalidOperationException("Cannot start."),
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service4.StartAsync(cancellationToken));
        await service4.DisposeAsync();
        Assert.Equal(ServiceState.Disposed, service2.State);

        var service5 = new Service(output);
        await service5.DisposeAsync();
        await service5.DisposeAsync();
        Assert.Equal(ServiceState.Disposed, service2.State);
    }

    [Fact]
    public async Task DisposeAsync_Transitioning()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service1 = new Service(output);
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 10),
            async (_, _) => await service1.DisposeAsync());
        Assert.Equal(ServiceState.Disposed, service1.State);

        var service2 = new Service(output);
        await Task.WhenAll(
            service2.StartAsync(cancellationToken),
            service2.DisposeAsync().AsTask());
        Assert.Equal(ServiceState.Disposed, service2.State);

        var service3 = new Service(output);
        await service3.StartAsync(cancellationToken);
        await Task.WhenAll(
            service3.StopAsync(cancellationToken),
            service3.DisposeAsync().AsTask());
        Assert.Equal(ServiceState.Disposed, service2.State);

        var service4 = new Service(output)
        {
            StartTask = _ => throw new InvalidOperationException("Cannot start."),
            RecoverTask = Task.Delay(100, cancellationToken),
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service4.StartAsync(cancellationToken));
        await Task.WhenAll(
            service4.RecoverAsync(),
            service4.DisposeAsync().AsTask());
        Assert.Equal(ServiceState.Disposed, service4.State);

        var service5 = new Service(output);
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
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var service1 = new Service(output);
        await service1.StartAsync(cancellationToken);
        var task1 = service1.DelayAsync(TimeSpan.FromSeconds(10), cancellationToken);
        await service1.StopAsync(cancellationToken);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task1);

        var service2 = new Service(output);
        await service2.StartAsync(cancellationToken);
        var task2 = service2.DelayAsync(TimeSpan.FromSeconds(10), cancellationToken);
        await service2.DisposeAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task2);
    }

    [Fact]
    public async Task CreateCancellationTokenSource_Throw_WhenStateIsNotStarted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var service = new Service(output);
        var e = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.DelayAsync(TimeSpan.FromSeconds(10), cancellationToken));
        Assert.StartsWith("Cannot create a cancellation token source", e.Message);
    }

    [Fact]
    public async Task CreateCancellationTokenSource_Throw_WhenDisposed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = new Service(output);
        await service.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await service.DelayAsync(TimeSpan.FromSeconds(10), cancellationToken));
    }

    private sealed class Service(ITestOutputHelper output)
        : ServiceBase(TestLogging.CreateLogger<Service>(output))
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
