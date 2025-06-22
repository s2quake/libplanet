#pragma warning disable S6966 // Awaitable method should be used
#pragma warning disable S2925 // "Thread.Sleep" should not be used in tests
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Lru;
using Libplanet.Net.Threading;
using Libplanet.TestUtilities;
using Nito.AsyncEx;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Threading;

public class DispatcherTest(ITestOutputHelper output)
{
    [Fact]
    public async Task Post()
    {
        await using var dispatcher = new Dispatcher();
        var manualResetEvent = new ManualResetEvent(initialState: false);
        dispatcher.Post(() => manualResetEvent.Set());
        var b = manualResetEvent.WaitOne(millisecondsTimeout: 1000);
        Assert.True(b);
    }

    [Fact]
    public async Task Invoke()
    {
        var dispatcher = new Dispatcher();
        var b = false;
        dispatcher.Invoke(() => b = true);
        Assert.True(b);

        var i = dispatcher.Invoke(() => 1);
        Assert.Equal(0, i);

        await dispatcher.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => dispatcher.Invoke(() => { }));
        Assert.Throws<ObjectDisposedException>(() => dispatcher.Invoke(() => 1));
    }

    private static async Task<int> InvokeAsync1()
    {
        await Task.Delay(10);
        return 42;
    }

    [Fact]
    public async Task InvokeAsync()
    {
        var dispatcher = new Dispatcher();
        var random = RandomUtility.GetRandom(output);
        var expected1 = RandomUtility.Try(random, RandomUtility.Int32, item => item != 0);
        var actual1 = 0;
        await dispatcher.InvokeAsync(() => { actual1 = expected1; });
        Assert.Equal(expected1, actual1);

        var expected2 = RandomUtility.Try(random, RandomUtility.Int32, item => item != 0);
        var actual2 = await dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(10);
            return expected2;
        });
        Assert.Equal(expected2, actual2);

        var expected3 = RandomUtility.Try(random, RandomUtility.Int32, item => item != 0);
        var actual3 = await dispatcher.InvokeAsync(() => expected3);
        Assert.Equal(expected3, actual3);

        await dispatcher.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await dispatcher.InvokeAsync(() => { }));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await dispatcher.InvokeAsync(() => { }, default));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await dispatcher.InvokeAsync(Task.FromResult(expected2)));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await dispatcher.InvokeAsync(() => 1));
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await dispatcher.InvokeAsync(() => 1, default));
    }

    [Fact]
    public async Task InvokeGenericAsync_WaitTest()
    {
        await using var dispatcher = new Dispatcher();
        var b = false;
        var task = dispatcher.InvokeAsync(() =>
        {
            Thread.Sleep(1000);
            b = true;
        });
        await Task.Delay(10);
        await dispatcher.DisposeAsync();
        await task;
        Assert.True(b);
    }

    [Fact]
    public async Task Post_FailTest()
    {
        await using var dispatcher = new Dispatcher();
        await dispatcher.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() =>
        {
            dispatcher.Post(() => { });
        });
    }
}
