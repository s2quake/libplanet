using System.Collections;

namespace Libplanet.TestUtilities;

public sealed class DisposerCollection(IEnumerable<IDisposable> disposables)
    : IEnumerable<IDisposable>, IDisposable
{
    private readonly List<IDisposable> _itemList = disposables.ToList();

    public void Dispose()
    {
        for (var i = _itemList.Count - 1; i >= 0; i--)
        {
            _itemList[i].Dispose();
        }
    }

    public IEnumerator<IDisposable> GetEnumerator() => _itemList.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _itemList.GetEnumerator();
}
