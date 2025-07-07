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

    public PeerState Head => _items.FirstOrDefault() ?? throw new InvalidOperationException("The bucket is empty.");

    public PeerState Tail => _items.LastOrDefault() ?? throw new InvalidOperationException("The bucket is empty.");

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

    public bool AddOrUpdate(Peer peer, DateTimeOffset timestamp)
    {
        var address = peer.Address;
        var peerState = new PeerState
        {
            Peer = peer,
            LastUpdated = timestamp,
        };

        if (_items.Count < _capacity || _itemByAddress.ContainsKey(address))
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
            throw new InvalidOperationException("No peers available to select from.", e);
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

    public void Check(Peer peer, TimeSpan latency)
    {
        var address = peer.Address;
        if (_itemByAddress.TryGetValue(address, out var peerState1))
        {
            var peerState2 = peerState1 with
            {
                Latency = latency,
            };

            _itemByAddress[address] = peerState2;
            _items = _items.Remove(peerState1).Add(peerState2);
        }
        else
        {
            throw new KeyNotFoundException($"Peer {peer} not found in the bucket.");
        }
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
