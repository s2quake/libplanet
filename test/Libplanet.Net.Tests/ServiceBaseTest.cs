using System.Threading;
using System.Threading.Tasks;
using Libplanet.Types.Threading;
using Nito.AsyncEx.Synchronous;

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
    public async Task RecoverAsync()
    {
        await using var service = new Service
        {
            StartTask = _ => throw new InvalidOperationException("Cannot start."),
        };
        await TaskUtility.TryWait(service.StartAsync(default));
        await service.RecoverAsync();
        Assert.Equal(ServiceState.None, service.State);
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

        await service.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(service.RecoverAsync);
    }

    private sealed class Service : ServiceBase
    {
        public Func<CancellationToken, Task> StartTask { get; init; } = _ => Task.Delay(100);

        public Func<CancellationToken, Task> StopTask { get; init; } = _ => Task.Delay(100);

        public Task RecoverTask { get; init; } = Task.Delay(100);

        public Task DisposeTask { get; init; } = Task.Delay(100);

        protected override Task OnRecoverAsync() => RecoverTask;

        protected override async Task OnStartAsync(CancellationToken cancellationToken)
            => await StartTask(cancellationToken);

        protected override async Task OnStopAsync(CancellationToken cancellationToken)
            => await StopTask(cancellationToken);

        protected override async ValueTask DisposeAsyncCore() => await DisposeTask;
    }
}
