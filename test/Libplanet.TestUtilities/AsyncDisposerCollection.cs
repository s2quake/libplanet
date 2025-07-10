using System.Collections;
using System.Threading.Tasks;

namespace Libplanet.TestUtilities;

public sealed class AsyncDisposerCollection(IEnumerable<IAsyncDisposable> disposables)
    : IEnumerable<IAsyncDisposable>, IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _itemList = [.. disposables];

    public async ValueTask DisposeAsync()
    {
        for (var i = _itemList.Count - 1; i >= 0; i--)
        {
            await _itemList[i].DisposeAsync();
        }
    }

    public IEnumerator<IAsyncDisposable> GetEnumerator() => _itemList.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _itemList.GetEnumerator();
}
