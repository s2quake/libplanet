using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net.Protocols;

internal sealed class KademliaBucket
{
    private readonly int _size;
    private readonly Random _random;
    private readonly ConcurrentDictionary<Peer, PeerState> _stateByPeer = new();
    private readonly ConcurrentDictionary<Peer, PeerState> _replacementCache = new();
    private PeerState? _head;
    private PeerState? _tail;

    public KademliaBucket(int size, Random random)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), $"The value of {nameof(size)} must be positive.");
        }

        _size = size;
        _random = random;
    }

    public int Count => _stateByPeer.Count;

    public bool IsEmpty => _stateByPeer.IsEmpty;

    public bool IsFull => _stateByPeer.Count >= _size;

    public PeerState Head => _head ??= GetHead(_stateByPeer);

    public PeerState Tail => _tail ??= GetTail(_stateByPeer);

    public IEnumerable<Peer> Peers => _stateByPeer.Keys;

    public IEnumerable<PeerState> PeerStates => _stateByPeer.Values;

    public IReadOnlyDictionary<Peer, PeerState> ReplacementCache => _replacementCache;

    public void AddPeer(Peer peer, DateTimeOffset timestamp)
    {
        var peerState = new PeerState
        {
            Peer = peer,
            LastUpdated = timestamp,
        };

        if (_stateByPeer.Count < _size || _stateByPeer.ContainsKey(peer))
        {
            _stateByPeer.AddOrUpdate(peer, peerState, (_, _) => peerState);
        }
        else if (_replacementCache.Count < _size || _replacementCache.ContainsKey(peer))
        {
            _replacementCache.AddOrUpdate(peer, peerState, (_, _) => peerState);
        }
        else
        {
            var oldestPeer = _replacementCache.OrderBy(ps => ps.Value.LastUpdated).First().Key;
            _replacementCache.TryRemove(oldestPeer, out var _);
            _replacementCache.AddOrUpdate(peer, peerState, (_, _) => peerState);
        }

        _head = null;
        _tail = null;
    }

    public bool Contains(Peer peer) => _stateByPeer.ContainsKey(peer);

    public void Clear()
    {
        _stateByPeer.Clear();
        _head = null;
        _tail = null;
    }

    public bool Remove(Peer peer)
    {
        var result = _stateByPeer.TryRemove(peer, out _);
        _head = null;
        _tail = null;
        return result;
    }

    public bool RemoveCache(Peer peer) => _replacementCache.TryRemove(peer, out _);

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

    public void Check(Peer peer, DateTimeOffset start, DateTimeOffset end)
    {
        if (_stateByPeer.TryGetValue(peer, out var peerState1))
        {
            var peerState2 = peerState1 with
            {
                LastChecked = start,
                Latency = end - start
            };
            _stateByPeer.TryUpdate(peer, peerState2, peerState1);
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
}
