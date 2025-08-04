using System.Collections;
using System.Threading;
using Libplanet.Types.Threading;

namespace Libplanet;

public abstract class ObjectValidatorCollectionBase<T> : IEnumerable<T>
    where T : IObjectValidator<T>
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly List<T> _itemList = [];

    public int Count
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _itemList.Count;
        }
    }

    public void Add(T item)
    {
        using var _ = _lock.WriteScope();
        _itemList.Add(item);
    }

    public void Remove(T item)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        _itemList.Remove(item);
    }

    public bool Contains(T item)
    {
        using var _ = _lock.ReadScope();
        return _itemList.Contains(item);
    }

    public IEnumerator<T> GetEnumerator()
    {
        using var _ = _lock.ReadScope();
        return _itemList.ToList().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}