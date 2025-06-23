#pragma warning disable S6966 // Awaitable method should be used
#pragma warning disable S2925 // "Thread.Sleep" should not be used in tests
#pragma warning disable S108 // Nested blocks of code should not be left empty
#pragma warning disable S1186 // Methods should not be empty
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Net.Threading;
using Libplanet.TestUtilities;
using Xunit.Abstractions;

namespace Libplanet.Net.Tests.Threading;

public class DispatcherTest(ITestOutputHelper output)
{
    [Fact]
    public async Task Post()
    {
        var dispatcher = new Dispatcher();
        var manualResetEvent = new ManualResetEvent(initialState: false);
        dispatcher.Post(() => manualResetEvent.Set());
        var b = manualResetEvent.WaitOne(millisecondsTimeout: 1000);
        Assert.True(b);

        await dispatcher.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => dispatcher.Post(() => { }));
    }

    [Fact]
    public async Task Invoke()
    {
        var dispatcher = new Dispatcher();
        var b = false;
        dispatcher.Invoke(() => b = true);
        Assert.True(b);

        var i = dispatcher.Invoke(() => 1);
        Assert.Equal(1, i);

        await dispatcher.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => dispatcher.Invoke(() => { }));
        Assert.Throws<ObjectDisposedException>(() => dispatcher.Invoke(() => 1));
    }

    [Fact]
    public async Task InvokeAsync_WithAction()
    {
        var dispatcher = new Dispatcher();
        var random = RandomUtility.GetRandom(output);
        var expected = RandomUtility.Try(random, RandomUtility.Int32, item => item != 0);
        var actual = 0;
        await dispatcher.InvokeAsync(() => { actual = expected; });
        Assert.Equal(expected, actual);

        static void Action() { }
        await dispatcher.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await dispatcher.InvokeAsync(Action));
    }

    [Fact]
    public async Task InvokeAsync_WithFunc()
    {
        var dispatcher = new Dispatcher();
        var random = RandomUtility.GetRandom(output);
        var expected = RandomUtility.Try(random, RandomUtility.Int32, item => item != 0);
        var actual = await dispatcher.InvokeAsync(() => expected);
        Assert.Equal(expected, actual);

        static int Func() => 1;
        await dispatcher.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await dispatcher.InvokeAsync(Func));
    }

    [Fact]
    public async Task InvokeAsync_WithActionTask()
    {
        var dispatcher = new Dispatcher();
        var random = RandomUtility.GetRandom(output);
        var expected = RandomUtility.Try(random, RandomUtility.Int32, item => item != 0);
        var actual = 0;
        await dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(1000);
            actual = expected;
        });
        Assert.Equal(expected, actual);

        var task = Task.CompletedTask;
        await dispatcher.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await dispatcher.InvokeAsync(async () => await task));
    }

    [Fact]
    public async Task InvokeAsync_WithFuncTask()
    {
        var dispatcher = new Dispatcher();
        var random = RandomUtility.GetRandom(output);
        var expected = RandomUtility.Try(random, RandomUtility.Int32, item => item != 0);
        var actual = await dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(10);
            return expected;
        });
        Assert.Equal(expected, actual);

        var task = Task.FromResult(1);
        await dispatcher.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await dispatcher.InvokeAsync(async () => await task));
    }

    [Fact]
    public async Task InvokeAsync_Cancel_WithAction()
    {
        await using var dispatcher = new Dispatcher();
        using var cancellationTokenSource = new CancellationTokenSource(10);
        static void Action(CancellationToken cancellationToken)
        {
            for (var i = 0; i < 1000; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new NotSupportedException("Cancellation requested");
                }

                Thread.Sleep(1);
            }
        }

        var e = await Assert.ThrowsAsync<NotSupportedException>(
            () => dispatcher.InvokeAsync(Action, cancellationTokenSource.Token));
        Assert.Equal("Cancellation requested", e.Message);
    }

    [Fact]
    public async Task InvokeAsync_Cancel_WithFunc()
    {
        await using var dispatcher = new Dispatcher();
        using var cancellationTokenSource = new CancellationTokenSource(10);
        static int Func(CancellationToken cancellationToken)
        {
            for (var i = 0; i < 1000; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new NotSupportedException("Cancellation requested");
                }

                Thread.Sleep(1);
            }

            return 1;
        }

        var e = await Assert.ThrowsAsync<NotSupportedException>(
            () => dispatcher.InvokeAsync(Func, cancellationTokenSource.Token));
        Assert.Equal("Cancellation requested", e.Message);
    }

    [Fact]
    public async Task InvokeAsync_Cancel_WithFuncTask()
    {
        await using var dispatcher = new Dispatcher();
        using var cancellationTokenSource = new CancellationTokenSource(10);
        static async Task<int> FuncTask(CancellationToken cancellationToken)
        {
            for (var i = 0; i < 1000; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new NotSupportedException("Cancellation requested");
                }

                await Task.Delay(1);
            }

            return 1;
        }

        var e = await Assert.ThrowsAsync<NotSupportedException>(
            () => dispatcher.InvokeAsync(FuncTask, cancellationTokenSource.Token));
        Assert.Equal("Cancellation requested", e.Message);
    }

    [Fact]
    public async Task InvokeAsync_Cancel_WithActionTask()
    {
        await using var dispatcher = new Dispatcher();
        using var cancellationTokenSource = new CancellationTokenSource(10);
        static async Task ActionTask(CancellationToken cancellationToken)
        {
            for (var i = 0; i < 1000; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new NotSupportedException("Cancellation requested");
                }

                await Task.Delay(1);
            }
        }

        var e = await Assert.ThrowsAsync<NotSupportedException>(
            () => dispatcher.InvokeAsync(ActionTask, cancellationTokenSource.Token));
        Assert.Equal("Cancellation requested", e.Message);
    }

    [Fact]
    public async Task InvokeAsync_CancelBeforeRun_WithAction()
    {
        await using var dispatcher = new Dispatcher();
        _ = dispatcher.InvokeAsync(() => Thread.Sleep(1000));

        using var cancellationTokenSource = new CancellationTokenSource(10);
        static void Action(CancellationToken cancellationToken)
            => throw new UnreachableException("This should not be called.");
        _ = Task.Run(async () =>
        {
            Thread.Sleep(100);
            await dispatcher.DisposeAsync();
        });

        var e = await Assert.ThrowsAsync<OperationCanceledException>(
            () => dispatcher.InvokeAsync(Action, cancellationTokenSource.Token));
        Assert.Equal("The operation was canceled before it could start.", e.Message);
    }

    [Fact]
    public async Task InvokeAsync_CancelBeforeRun_WithFunc()
    {
        await using var dispatcher = new Dispatcher();
        _ = dispatcher.InvokeAsync(() => Thread.Sleep(1000));

        using var cancellationTokenSource = new CancellationTokenSource(10);
        static int Func(CancellationToken cancellationToken)
            => throw new UnreachableException("This should not be called.");
        _ = Task.Run(async () =>
        {
            Thread.Sleep(100);
            await dispatcher.DisposeAsync();
        });

        var e = await Assert.ThrowsAsync<OperationCanceledException>(
            () => dispatcher.InvokeAsync(Func, cancellationTokenSource.Token));
        Assert.Equal("The operation was canceled before it could start.", e.Message);
    }

    [Fact]
    public async Task InvokeAsync_CancelBeforeRun_WithActionTask()
    {
        await using var dispatcher = new Dispatcher();
        _ = dispatcher.InvokeAsync(() => Thread.Sleep(1000));

        using var cancellationTokenSource = new CancellationTokenSource(10);
        static async Task ActionTask(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            throw new UnreachableException("This should not be called.");
        }

        _ = Task.Run(async () =>
        {
            Thread.Sleep(100);
            await dispatcher.DisposeAsync();
        });

        var e = await Assert.ThrowsAsync<OperationCanceledException>(
            () => dispatcher.InvokeAsync(ActionTask, cancellationTokenSource.Token));
        Assert.Equal("The operation was canceled before it could start.", e.Message);
    }

    [Fact]
    public async Task InvokeAsync_CancelBeforeRun_WithFuncTask()
    {
        await using var dispatcher = new Dispatcher();
        _ = dispatcher.InvokeAsync(() => Thread.Sleep(1000));

        using var cancellationTokenSource = new CancellationTokenSource(10);
        static async Task<int> FuncTask(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            throw new UnreachableException("This should not be called.");
        }

        _ = Task.Run(async () =>
        {
            Thread.Sleep(100);
            await dispatcher.DisposeAsync();
        });

        var e = await Assert.ThrowsAsync<OperationCanceledException>(
            () => dispatcher.InvokeAsync(FuncTask, cancellationTokenSource.Token));
        Assert.Equal("The operation was canceled before it could start.", e.Message);
    }

    [Fact]
    public async Task Invoke_InSequence()
    {
        await using var dispatcher = new Dispatcher();
        var list = new List<int>();
        var random = RandomUtility.GetRandom(output);

        var funcs = new Func<int, Task>[]
        {
            (i) =>
            {
                dispatcher.Invoke(() =>
                {
                    list.Add(i);
                });
                return Task.CompletedTask;
            },
            (i) =>
            {
                dispatcher.Invoke(() =>
                {
                    list.Add(i);
                    return list.Count;
                });
                return Task.CompletedTask;
            },
            (i) => dispatcher.InvokeAsync(() => list.Add(i)),
            (i) =>
            {
                return dispatcher.InvokeAsync(() =>
                {
                    list.Add(i);
                    return list.Count;
                });
            },
            (i) => dispatcher.InvokeAsync(async () =>
            {
                var random = new Random(i);
                await Task.Delay(random.Next(1000));
                list.Add(i);
            }),
            (i) => dispatcher.InvokeAsync(async () =>
            {
                var random = new Random(i);
                await Task.Delay(random.Next(1000));
                list.Add(i);
                return list.Count;
            }),
        };

        var indices = Enumerable.Range(0, funcs.Length).ToArray();
        random.Shuffle(indices);

        var taskList = new List<Task>();
        for (var i = 0; i < indices.Length; i++)
        {
            var index = indices[i];
            taskList.Add(funcs[index](index));
            await Task.Yield();
        }

        await Task.WhenAll(taskList);

        Assert.Equal(indices, list);
    }
}
