using System.Threading.Tasks;

namespace Libplanet.Net;

public sealed class AsyncDelegate<T>
{
    private IEnumerable<Func<T, Task>> _functions = [];

    public void Register(Func<T, Task> func)
    {
        _functions = _functions.Append(func);

    }

    public void Unregister(Func<T, Task> func)
    {
        _functions = _functions.Where(f => !f.Equals(func));
    }

    public async Task InvokeAsync(T arg)
    {
        IEnumerable<Task> tasks = _functions.Select(f => f(arg));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
