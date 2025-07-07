using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net.Protocols;

internal sealed class Bucket(int capacity) : IReadOnlyDictionary<Peer, PeerState>
{
    private readonly int _capacity = ValidateSize(capacity);
    private readonly Random _random = new();
    private readonly ConcurrentDictionary<Peer, PeerState> _stateByPeer = new();
    private PeerState? _head;
    private PeerState? _tail;

    public int Count => _stateByPeer.Count;

    public bool IsEmpty => _stateByPeer.IsEmpty;

    public bool IsFull => _stateByPeer.Count >= _capacity;

    public PeerState Head => _head ??= GetHead(_stateByPeer);

    public PeerState Tail => _tail ??= GetTail(_stateByPeer);

    public IEnumerable<Peer> Keys => _stateByPeer.Keys;

    public IEnumerable<PeerState> Values => _stateByPeer.Values;

    public PeerState this[Peer key] => _stateByPeer[key];

    public bool AddOrUpdate(Peer peer, DateTimeOffset timestamp)
    {
        var state = new PeerState
        {
            Peer = peer,
            LastUpdated = timestamp,
        };

        if (_stateByPeer.Count < _capacity || _stateByPeer.ContainsKey(peer))
        {
            _stateByPeer.AddOrUpdate(peer, state, (_, _) => state);
            _head = null;
            _tail = null;
            return true;
        }

        return false;
    }

    public bool ContainsKey(Peer key) => _stateByPeer.ContainsKey(key);

    public void Clear()
    {
        _stateByPeer.Clear();
        _head = null;
        _tail = null;
    }

    public bool Remove(Peer key)
    {
        var result = _stateByPeer.TryRemove(key, out _);
        _head = null;
        _tail = null;
        return result;
    }

    public Peer GetRandomPeer(Address except)
    {
        var query = from peer in _stateByPeer.Keys
                    where peer.Address != except
                    orderby _random.Next()
                    select peer;

        try
        {
            return query.First();
        }
        catch (InvalidOperationException e)
        {
            throw new InvalidOperationException("No peers available to select from.", e);
        }
    }

    public bool TryGetRandomPeer(Address except, [MaybeNullWhen(false)] out Peer value)
    {
        var query = from peer in _stateByPeer.Keys
                    where peer.Address != except
                    orderby _random.Next()
                    select peer;

        value = query.FirstOrDefault();
        return value is not null;
    }

    public void Check(Peer peer, DateTimeOffset start, TimeSpan latency)
    {
        if (_stateByPeer.TryGetValue(peer, out var peerState1))
        {
            var peerState2 = peerState1 with
            {
                LastChecked = start,
                Latency = latency,
            };
            _stateByPeer.TryUpdate(peer, peerState2, peerState1);
        }
        else
        {
            throw new KeyNotFoundException($"Peer {peer} not found in the bucket.");
        }
    }

    private static PeerState GetHead(ConcurrentDictionary<Peer, PeerState> peerStates)
    {
        if (peerStates.IsEmpty)
        {
            throw new InvalidOperationException("The bucket is empty.");
        }

        return peerStates.Values.OrderBy(ps => ps.LastUpdated).First();
    }

    private static PeerState GetTail(ConcurrentDictionary<Peer, PeerState> peerStates)
    {
        if (peerStates.IsEmpty)
        {
            throw new InvalidOperationException("The bucket is empty.");
        }

        return peerStates.Values.OrderByDescending(ps => ps.LastUpdated).First();
    }

    private static int ValidateSize(int size)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), $"The value of {nameof(size)} must be positive.");
        }

        return size;
    }

    public bool TryGetValue(Peer key, [MaybeNullWhen(false)] out PeerState value) => _stateByPeer.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<Peer, PeerState>> GetEnumerator()
    {
        foreach (var pair in _stateByPeer)
        {
            yield return pair;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
