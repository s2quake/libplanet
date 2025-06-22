#pragma warning disable S6966 // Awaitable method should be used
#pragma warning disable S2925 // "Thread.Sleep" should not be used in tests
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Threading;

namespace Libplanet.Net.Tests.Threading;

public class DispatcherTest
{
    [Fact]
    public async Task Invoke_Test()
    {
        await using var dispatcher = new Dispatcher();
        var b = false;
        dispatcher.Invoke(() => b = true);
        Assert.True(b);

        var i = dispatcher.Invoke(() => 1);
        Assert.Equal(0, i);
    }

    [Fact]
    public async Task Invoke_FailTest()
    {
        var dispatcher = new Dispatcher();
        await dispatcher.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => dispatcher.Invoke(() => { }));
        Assert.Throws<ObjectDisposedException>(() => dispatcher.Invoke(() => 1));
    }

    //     [Fact]
    //     public async Task InvokeGeneric_Test()
    //     {
    //         await using var dispatcher = new Dispatcher();
    // #pragma warning disable S6966 // Awaitable method should be used
    //         var v = dispatcher.Invoke(() => 0);
    // #pragma warning restore S6966 // Awaitable method should be used
    //         Assert.Equal(0, v);
    //     }

    // [Fact]
    // public void InvokeGeneric_FailTest()
    // {
    //     var dispatcher = new Dispatcher();
    //     dispatcher.Dispose();
    //     Assert.Throws<ObjectDisposedException>(() =>
    //     {
    //         dispatcher.Invoke(() => 0);
    //     });
    // }

    [Fact]
    public async Task InvokeAsync_Test()
    {
        await using var dispatcher = new Dispatcher();
        var b = false;
        await dispatcher.InvokeAsync(() => b = true);
        Assert.True(b);

        var i = await dispatcher.InvokeAsync(() => 1);
        Assert.Equal(1, i);
    }

    [Fact]
    public async Task InvokeAsync_FailTest()
    {
        await using var dispatcher = new Dispatcher();
        await dispatcher.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await dispatcher.InvokeAsync(() => { }));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await dispatcher.InvokeAsync(() => 1));
    }

    // [Fact]
    // public async Task InvokeGenericAsync_Test()
    // {
    //     var dispatcher = new Dispatcher();
    //     var v = await dispatcher.InvokeAsync(() => 1);
    //     Assert.Equal(1, v);
    // }

    // [Fact]
    // public async Task InvokeGenericAsync_FailTest()
    // {
    //     var dispatcher = new Dispatcher();
    //     dispatcher.Dispose();
    //     await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
    //     {
    //         await dispatcher.InvokeAsync(() => 1);
    //     });
    // }

    // "Thread.Sleep" should not be used in tests

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
    public async Task Post_Test()
    {
        await using var dispatcher = new Dispatcher();
        var manualResetEvent = new ManualResetEvent(initialState: false);
        dispatcher.Post(() => manualResetEvent.Set());
        var b = manualResetEvent.WaitOne(millisecondsTimeout: 1000);
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
