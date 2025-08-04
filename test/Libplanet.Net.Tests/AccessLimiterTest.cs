namespace Libplanet.Net.Tests;

public class AccessLimiterTest
{
    [Fact]
    public async Task WaitAsync()
    {
        var limiter = new AccessLimiter(3);
        int count = 0;
        async Task SampleTask(AccessLimiter sema)
        {
            if (await sema.CanAccessAsync(default) is not null)
            {
                await Task.Delay(1000);
                Interlocked.Increment(ref count);
            }
        }

        var tasks = new List<Task>
        {
            SampleTask(limiter),
            SampleTask(limiter),
            SampleTask(limiter),
            SampleTask(limiter),
        };
        await Task.WhenAll(tasks);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task WaitAsyncZero()
    {
        var limiter = new AccessLimiter(0);
        int count = 0;
        async Task SampleTask(AccessLimiter sema)
        {
            if (await sema.CanAccessAsync(default) is not null)
            {
                await Task.Delay(1000);
                Interlocked.Increment(ref count);
            }
        }

        var tasks = new List<Task>
        {
            SampleTask(limiter),
            SampleTask(limiter),
            SampleTask(limiter),
        };
        await Task.WhenAll(tasks);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task Release()
    {
        var limiter = new AccessLimiter(3);
        int count = 0;
        async Task SampleTask(AccessLimiter sema)
        {
            using var _ = await sema.CanAccessAsync(default);
            await Task.Delay(1000);
            Interlocked.Increment(ref count);
        }

        var tasks = new List<Task>
        {
            SampleTask(limiter),
            SampleTask(limiter),
            SampleTask(limiter),
        };
        await Task.WhenAll(tasks);
        await SampleTask(limiter);

        Assert.Equal(4, count);
    }
}
