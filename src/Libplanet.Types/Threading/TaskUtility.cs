using System.Threading;
using System.Threading.Tasks;

namespace Libplanet.Types.Threading;

public static class TaskUtility
{
    public static async Task<bool> TryDelay(int millisecondsDelay, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            await Task.Delay(millisecondsDelay, cancellationToken);
            return true;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    public static async Task<bool> TryDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            await Task.Delay(delay, cancellationToken);
            return true;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    public static async Task<bool> TryWait(Task task)
    {
        try
        {
            await task;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static async Task<bool> TryWhenAll(params Task[] tasks)
    {
        try
        {
            await Task.WhenAll(tasks);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static async Task<bool> TryWhenAll(IEnumerable<Task> tasks)
    {
        try
        {
            await Task.WhenAll(tasks);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}