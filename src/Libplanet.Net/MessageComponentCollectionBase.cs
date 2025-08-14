using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Net;

public abstract class MessageComponentCollectionBase<T>(Func<T, Type> getMessageType) : IEnumerable<T>
{
    private readonly Dictionary<Type, ImmutableArray<T>> _itemsByType = [];
    private readonly Dictionary<Type, ImmutableArray<T>> _allByType = [];
    private int _count;

    public int Count => _count;

    public ImmutableArray<T> this[Type messageType] => _itemsByType[messageType];

    public void Add(T item)
    {
        var messageType = getMessageType(item);
        if (!_itemsByType.TryGetValue(messageType, out var items))
        {
            items = [];
        }

        items = items.Add(item);
        _itemsByType[messageType] = items;
        _count++;
        ClearCache(messageType);
    }

    public bool Remove(T item)
    {
        var messageType = getMessageType(item);
        if (_itemsByType.TryGetValue(messageType, out var items) && items.Contains(item))
        {
            items = items.Remove(item);
            if (items.IsEmpty)
            {
                _itemsByType.Remove(messageType);
            }
            else
            {
                _itemsByType[messageType] = items;
            }

            _count--;
            ClearCache(messageType);
            return true;
        }

        return false;
    }

    public bool Contains(Type messageType) => _itemsByType.ContainsKey(messageType);

    public bool TryGetValidator(Type messageType, [MaybeNullWhen(false)] out ImmutableArray<T> items)
        => _itemsByType.TryGetValue(messageType, out items);

    public void Clear()
    {
        _itemsByType.Clear();
        _count = 0;
    }

    public ImmutableArray<T> GetAll(Type messageType)
    {
        if (!_allByType.TryGetValue(messageType, out var items))
        {
            items = [.. Enumerate(messageType)];
            _allByType[messageType] = items;
        }

        return items;
    }

    public IEnumerator<T> GetEnumerator()
    {
        foreach (var items in _itemsByType.Values)
        {
            foreach (var item in items)
            {
                yield return item;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private IEnumerable<T> Enumerate(Type? messageType)
    {
        while (messageType is not null && typeof(IMessage).IsAssignableFrom(messageType))
        {
            if (_itemsByType.TryGetValue(messageType, out var items1))
            {
                foreach (var item in items1)
                {
                    yield return item;
                }
            }

            messageType = messageType.BaseType;
        }

        if (_itemsByType.TryGetValue(typeof(IMessage), out var items2))
        {
            foreach (var item in items2)
            {
                yield return item;
            }
        }
    }

    private void ClearCache(Type? messageType)
    {
        while (messageType is not null && typeof(IMessage).IsAssignableFrom(messageType))
        {
            _allByType.Remove(messageType);

            messageType = messageType.BaseType;
        }

        _allByType.Remove(typeof(IMessage));
    }
}
