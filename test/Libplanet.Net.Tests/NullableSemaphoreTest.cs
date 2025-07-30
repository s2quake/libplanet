using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Net.Tests
{
    public class NullableSemaphoreTest
    {
        [Fact]
        public async Task WaitAsync()
        {
            var semaphore = new NullableSemaphore(3);
            int count = 0;
            async Task SampleTask(NullableSemaphore sema)
            {
                if (await sema.WaitAsync(TimeSpan.Zero, default))
                {
                    await Task.Delay(1000);
                    Interlocked.Increment(ref count);
                }
            }

            var tasks = new List<Task>
            {
                SampleTask(semaphore),
                SampleTask(semaphore),
                SampleTask(semaphore),
                SampleTask(semaphore),
            };
            await Task.WhenAll(tasks);

            Assert.Equal(3, count);
        }

        [Fact]
        public async Task WaitAsyncZero()
        {
            var semaphore = new NullableSemaphore(0);
            int count = 0;
            async Task SampleTask(NullableSemaphore sema)
            {
                if (await sema.WaitAsync(TimeSpan.Zero, default))
                {
                    await Task.Delay(1000);
                    Interlocked.Increment(ref count);
                }
            }

            var tasks = new List<Task>
            {
                SampleTask(semaphore),
                SampleTask(semaphore),
                SampleTask(semaphore),
            };
            await Task.WhenAll(tasks);

            Assert.Equal(3, count);
        }

        [Fact]
        public async Task Release()
        {
            var semaphore = new NullableSemaphore(3);
            int count = 0;
            async Task SampleTask(NullableSemaphore sema)
            {
                if (await sema.WaitAsync(TimeSpan.Zero, default))
                {
                    await Task.Delay(1000);
                    Interlocked.Increment(ref count);
                    sema.Release();
                }
            }

            var tasks = new List<Task>
            {
                SampleTask(semaphore),
                SampleTask(semaphore),
                SampleTask(semaphore),
            };
            await Task.WhenAll(tasks);
            await SampleTask(semaphore);

            Assert.Equal(4, count);
        }
    }
}
