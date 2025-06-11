using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Libplanet.Net.Protocols;

internal sealed class KBucketDictionary : IReadOnlyDictionary<Peer, PeerState>
{
    private readonly int _size;
    private readonly bool _replace;
    private readonly ConcurrentDictionary<Peer, PeerState> _stateByPeer;

    public KBucketDictionary(int size, bool replace)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                $"The value of {nameof(size)} must be positive.");
        }

        _size = size;
        _replace = replace;
        _stateByPeer = [];
    }

    public int Count => _stateByPeer.Count;

    public PeerState? Head { get; private set; }

    public PeerState? Tail { get; private set; }

    public IEnumerable<Peer> Keys => _stateByPeer.Keys;

    public IEnumerable<PeerState> Values => _stateByPeer.Values;

    public PeerState this[Peer key] => _stateByPeer[key];

    public bool AddOrUpdate(Peer peer)
        => AddOrUpdate(new PeerState { Peer = peer, LastUpdated = DateTimeOffset.UtcNow });

    public bool AddOrUpdate(PeerState peerState)
    {
        var peer = peerState.Peer;
        if (_stateByPeer.ContainsKey(peer))
        {
            _stateByPeer[peer] = peerState;
            UpdateHeadAndTail();
            return true;
        }
        else
        {
            if (_stateByPeer.Count < _size)
            {
                _stateByPeer[peer] = peerState;
                UpdateHeadAndTail();
                return true;
            }
            else
            {
                if (_replace)
                {
                    // Tail is never null since the dictionary size is always positive.
                    _stateByPeer.TryRemove(Tail!.Peer, out var _);
                    _stateByPeer[peer] = peerState;
                    UpdateHeadAndTail();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }

    public bool Remove(Peer peer)
    {
        var result = _stateByPeer.TryRemove(peer, out _);
        UpdateHeadAndTail();
        return result;
    }

    public void Clear()
    {
        _stateByPeer.Clear();
        UpdateHeadAndTail();
    }

    public bool ContainsKey(Peer key) => _stateByPeer.ContainsKey(key);

    public bool TryGetValue(Peer key, [MaybeNullWhen(false)] out PeerState value)
        => _stateByPeer.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<Peer, PeerState>> GetEnumerator() => _stateByPeer.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _stateByPeer.GetEnumerator();

    private void UpdateHeadAndTail()
    {
        if (_stateByPeer.Count is 0)
        {
            Head = null;
            Tail = null;
        }
        else if (_stateByPeer.Count is 1)
        {
            Head = Tail = _stateByPeer.Values.First();
        }
        else
        {
            var maxValue = DateTimeOffset.MaxValue;
            var minValue = DateTimeOffset.MinValue;
            foreach (var state in _stateByPeer.Values)
            {
                if (state.LastUpdated < maxValue)
                {
                    minValue = state.LastUpdated;
                    Head = state;
                }

                if (state.LastUpdated > minValue)
                {
                    maxValue = state.LastUpdated;
                    Tail = state;
                }
            }
        }
    }
}
