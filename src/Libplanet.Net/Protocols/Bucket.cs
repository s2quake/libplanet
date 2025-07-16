using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net.Protocols;

internal sealed class Bucket(int capacity) : IEnumerable<PeerState>
{
    private readonly int _capacity = ValidateCapacity(capacity);
    private readonly Random _random = new();
    private readonly Dictionary<Address, PeerState> _itemByAddress = [];
    private ImmutableSortedSet<PeerState> _items = [];

    public int Count => _items.Count;

    public bool IsEmpty => _items.IsEmpty;

    public bool IsFull => _items.Count == _capacity;

    public IEnumerable<Peer> Peers => _items.Select(item => item.Peer);

    public PeerState Newest => _items.LastOrDefault() ?? throw new InvalidOperationException("The bucket is empty.");

    public PeerState Oldest => _items.FirstOrDefault() ?? throw new InvalidOperationException("The bucket is empty.");

    public PeerState this[int index] => _items[index];

    public PeerState this[Peer peer]
    {
        get
        {
            var peerState = _itemByAddress[peer.Address];
            if (peerState.Peer != peer)
            {
                throw new KeyNotFoundException($"Peer {peer} not found in the bucket.");
            }

            return peerState;
        }
    }

    public PeerState this[Address address] => _itemByAddress[address];

    public bool AddOrUpdate(PeerState peerState)
    {
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
        if (_itemByAddress.TryGetValue(peer.Address, out var peerState))
        {
            return peerState.Peer == peer;
        }

        return false;
    }

    public bool Contains(Address address) => _itemByAddress.ContainsKey(address);

    public void Clear()
    {
        _items = [];
        _itemByAddress.Clear();
    }

    public bool Remove(Peer peer)
    {
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
        if (_itemByAddress.TryGetValue(address, out var peerState))
        {
            _itemByAddress.Remove(address);
            _items = _items.Remove(peerState);
            return true;
        }

        return false;
    }

    public bool TryGetValue(Address address, [MaybeNullWhen(false)] out PeerState value)
        => _itemByAddress.TryGetValue(address, out value);

    public bool TryGetPeer(Address address, [MaybeNullWhen(false)] out Peer value)
    {
        if (_itemByAddress.TryGetValue(address, out var peerState))
        {
            value = peerState.Peer;
            return true;
        }

        value = default;
        return false;
    }

    public Peer GetRandomPeer(Address except)
    {
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

    public bool TryGetRandomPeer(Address except, [MaybeNullWhen(false)] out Peer value)
    {
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
        foreach (var item in _items)
        {
            yield return item;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static int ValidateCapacity(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        return capacity;
    }
}
