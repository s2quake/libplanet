using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Libplanet.Types;
using Libplanet.Types.Threading;

namespace Libplanet.Net.Protocols;

internal sealed class Bucket(int capacity) : IEnumerable<PeerState>
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly int _capacity = ValidateCapacity(capacity);
    private readonly Random _random = new();
    private readonly Dictionary<Address, PeerState> _itemByAddress = [];
    private ImmutableSortedSet<PeerState> _items = [];

    public int Count
    {
        get
        {
            using var scope = new ReadScope(_lock);
            return _items.Count;
        }
    }

    public bool IsEmpty
    {
        get
        {
            using var scope = new ReadScope(_lock);
            return _items.IsEmpty;
        }
    }

    public bool IsFull
    {
        get
        {
            using var scope = new ReadScope(_lock);
            return _items.Count == _capacity;
        }
    }

    public IEnumerable<Peer> Peers
    {
        get
        {
            using var scope = new ReadScope(_lock);
            return _items.Select(item => item.Peer).ToArray();
        }
    }

    public PeerState Newest
    {
        get
        {
            using var scope = new ReadScope(_lock);
            return _items.LastOrDefault() ?? throw new InvalidOperationException("The bucket is empty.");
        }
    }

    public PeerState Oldest
    {
        get
        {
            using var scope = new ReadScope(_lock);
            return _items.FirstOrDefault() ?? throw new InvalidOperationException("The bucket is empty.");
        }
    }

    public PeerState this[int index]
    {
        get
        {
            using var scope = new ReadScope(_lock);
            return _items[index];
        }
    }

    public PeerState this[Peer peer]
    {
        get
        {
            using var scope = new ReadScope(_lock);
            var peerState = _itemByAddress[peer.Address];
            if (peerState.Peer != peer)
            {
                throw new KeyNotFoundException($"Peer {peer} not found in the bucket.");
            }

            return peerState;
        }
    }

    public PeerState this[Address address]
    {
        get
        {
            using var scope = new ReadScope(_lock);
            return _itemByAddress[address];
        }
    }

    public bool AddOrUpdate(PeerState peerState)
    {
        using var scope = new WriteScope(_lock);
        var address = peerState.Address;
        if (_itemByAddress.TryGetValue(address, out var value))
        {
            _itemByAddress[address] = peerState;
            _items = _items.Remove(value).Add(peerState);
            return true;
        }
        else if (_items.Count < _capacity)
        {
            _itemByAddress[address] = peerState;
            _items = _items.Add(peerState);
            return true;
        }

        return false;
    }

    public bool Contains(Peer peer)
    {
        using var scope = new ReadScope(_lock);
        if (_itemByAddress.TryGetValue(peer.Address, out var peerState))
        {
            return peerState.Peer == peer;
        }

        return false;
    }

    public void Clear()
    {
        using var scope = new WriteScope(_lock);
        _items = [];
        _itemByAddress.Clear();
    }

    public bool Remove(Peer peer)
    {
        using var scope = new WriteScope(_lock);
        var address = peer.Address;
        if (_itemByAddress.TryGetValue(address, out var peerState) && peerState.Peer.Address == address)
        {
            _itemByAddress.Remove(address);
            _items = _items.Remove(peerState);
            return true;
        }

        return false;
    }

    public bool Remove(Address address)
    {
        using var scope = new WriteScope(_lock);
        if (_itemByAddress.TryGetValue(address, out var peerState))
        {
            _itemByAddress.Remove(address);
            _items = _items.Remove(peerState);
            return true;
        }

        return false;
    }

    public Peer GetRandomPeer(Address except)
    {
        using var scope = new ReadScope(_lock);
        var query = from item in _items
                    where item.Address != except
                    orderby _random.Next()
                    select item;

        try
        {
            return query.First().Peer;
        }
        catch (InvalidOperationException e)
        {
            throw new InvalidOperationException($"No peer found in the bucket except {except}.", e);
        }
    }

    public bool TryGetPeer(Address address, [MaybeNullWhen(false)] out Peer value)
    {
        using var scope = new ReadScope(_lock);
        if (_itemByAddress.TryGetValue(address, out var peerState))
        {
            value = peerState.Peer;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGetRandomPeer(Address except, [MaybeNullWhen(false)] out Peer value)
    {
        using var scope = new ReadScope(_lock);
        var query = from item in _items
                    where item.Address != except
                    orderby _random.Next()
                    select item;

        if (query.FirstOrDefault() is { } peerState)
        {
            value = peerState.Peer;
            return true;
        }

        value = default;
        return false;
    }

    public IEnumerator<PeerState> GetEnumerator()
    {
        using var scope = new ReadScope(_lock);
        var items = _items.ToArray();
        return ((IEnumerable<PeerState>)items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static int ValidateCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        return capacity;
    }
}
