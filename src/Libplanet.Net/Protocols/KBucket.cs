using Libplanet.Types;

namespace Libplanet.Net.Protocols;

internal sealed class KBucket
{
    private readonly int _size;
    private readonly Random _random;
    private readonly KBucketDictionary _peerStates;
    private readonly KBucketDictionary _replacementCache;

    public KBucket(int size, Random random)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                $"The value of {nameof(size)} must be positive.");
        }

        _size = size;
        _random = random;
        _peerStates = new KBucketDictionary(_size, false);
        _replacementCache = new KBucketDictionary(_size, true);
    }

    public int Count => _peerStates.Count;

    public bool IsEmpty => _peerStates.Count == 0;

    public bool IsFull => _peerStates.Count >= _size;

    public PeerState? Head => _peerStates.Head;

    public PeerState? Tail => _peerStates.Tail;

    public IEnumerable<Peer> Peers => _peerStates.Keys;

    public IEnumerable<PeerState> PeerStates => _peerStates.Values;

    public KBucketDictionary ReplacementCache => _replacementCache;

    public void AddPeer(Peer peer, DateTimeOffset updated)
    {
        var peerState = new PeerState
        {
            Peer = peer,
            LastUpdated = updated
        };
        if (!_peerStates.AddOrUpdate(peerState))
        {
            ReplacementCache.AddOrUpdate(peerState);
        }
    }

    public bool Contains(Peer peer)
    {
        return _peerStates.ContainsKey(peer);
    }

    public void Clear()
    {
        _peerStates.Clear();
    }

    public bool RemovePeer(Peer peer)
    {
        if (_peerStates.Remove(peer))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public Peer? GetRandomPeer(Address? except = null)
    {
        List<Peer> peers = _peerStates.Keys
            .Where(p => except is not Address e || !p.Address.Equals(e))
            .ToList();
        return peers.Count > 0 ? peers[_random.Next(peers.Count)] : null;
    }

    public void Check(Peer peer, DateTimeOffset start, DateTimeOffset end)
    {
        if (_peerStates.TryGetValue(peer, out var peerState))
        {
            _peerStates.AddOrUpdate(peerState with
            {
                LastChecked = start,
                Latency = end - start
            });
        }
    }
}
