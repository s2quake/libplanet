using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Subjects;
using Libplanet.Types;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public sealed class PeerCollection(
    Address owner,
    int bucketCount = PeerCollection.BucketCount,
    int capacityPerBucket = PeerCollection.CapacityPerBucket)
    : IPeerCollection
{
    public const int BucketCount = Address.Size * 8;
    public const int CapacityPerBucket = 16;
    private readonly Subject<Peer> _addedSubject = new();
    private readonly Subject<Peer> _updatedSubject = new();
    private readonly Subject<Peer> _removedSubject = new();
    private readonly Subject<Unit> _clearedSubject = new();
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly BucketCollection _buckets = new(owner, bucketCount, capacityPerBucket);
    private ImmutableArray<IBucket>? _bucketArray;

    public PeerCollection(
        Peer owner,
        int bucketCount = BucketCount,
        int capacityPerBucket = CapacityPerBucket)
        : this(owner.Address, bucketCount, capacityPerBucket)
    {
    }

    public IObservable<Peer> Added => _addedSubject;

    public IObservable<Peer> Updated => _updatedSubject;

    public IObservable<Peer> Removed => _removedSubject;

    public IObservable<Unit> Cleared => _clearedSubject;

    public Address Owner => owner;

    public BucketCollection Buckets => _buckets;

    public int Count
    {
        get
        {
            using var _ = new ReadScope(_lock);
            return _buckets.Sum(item => item.Count);
        }
    }

    ImmutableArray<IBucket> IPeerCollection.Buckets => _bucketArray ??= [.. _buckets.Cast<IBucket>()];

    public Peer this[Address address] => _buckets[address][address].Peer;

    public void Add(Peer peer)
    {
        using var _ = _lock.WriteScope();
        AddInternal(peer);
    }

    public void AddMany(ImmutableArray<Peer> peers)
    {
        using var _ = _lock.WriteScope();
        foreach (var peer in peers)
        {
            AddInternal(peer);
        }
    }

    public bool AddOrUpdate(Peer peer)
        => AddOrUpdate(new PeerState { Peer = peer, LastUpdated = DateTimeOffset.UtcNow });

    public bool Remove(Address address) => address != owner && _buckets[address].Remove(address);

    public bool Remove(Peer peer)
    {
        using var _ = _lock.WriteScope();
        return RemoveInternal(peer);
    }

    public bool Contains(Address address) => address != owner && _buckets[address].Contains(address);

    public bool Contains(Peer peer)
    {
        using var _ = _lock.ReadScope();
        var address = peer.Address;
        if (address == owner)
        {
            return false;
        }

        var bucket = _buckets[address];
        if (bucket.TryGetValue(address, out var peerState) && peerState.Peer == peer)
        {
            return true;
        }

        return false;
    }

    public bool TryGetValue(Address address, [MaybeNullWhen(false)] out Peer peer)
    {
        using var _ = _lock.ReadScope();
        if (address == owner)
        {
            peer = default;
            return false;
        }

        return _buckets[address].TryGetPeer(address, out peer);
    }

    public bool TryGetPeerState(Peer peer, [MaybeNullWhen(false)] out PeerState peerState)
    {
        using var _ = _lock.ReadScope();
        var address = peer.Address;
        if (address == owner)
        {
            peerState = default;
            return false;
        }

        var bucket = _buckets[address];
        if (bucket.TryGetValue(address, out peerState) && peerState.Peer == peer)
        {
            return true;
        }

        peerState = default;
        return false;
    }

    public void Clear()
    {
        using var _ = _lock.WriteScope();
        foreach (var bucket in _buckets)
        {
            bucket.Clear();
        }

        _clearedSubject.OnNext(Unit.Default);
    }

    public int GetBucketIndex(Address address) => _buckets.IndexOf(address);

    public bool Ban(Address address)
    {
        if (address == Owner)
        {
            return false;
        }

        using var _ = _lock.WriteScope();
        if (_buckets[address].TryGetValue(address, out var peerState) && !peerState.IsBanned)
        {
            _buckets[address].AddOrUpdate(peerState with { IsBanned = true });
            return true;
        }

        return false;
    }

    public bool Unban(Address address)
    {
        using var _ = _lock.WriteScope();
        if (_buckets[address].TryGetValue(address, out var peerState) && peerState.IsBanned)
        {
            _buckets[address].AddOrUpdate(peerState with { IsBanned = false });
            return true;
        }

        return false;
    }

    public bool IsBanned(Address address)
    {
        using var _ = _lock.ReadScope();
        return _buckets[address].TryGetValue(address, out var peerState) && peerState.IsBanned;
    }

    public IEnumerator<Peer> GetEnumerator()
    {
        using var _ = _lock.ReadScope();
        foreach (var bucket in _buckets)
        {
            foreach (var peerState in bucket)
            {
                yield return peerState.Peer;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal PeerCollection CreateEmpty() => new(owner, _buckets.Count, _buckets.CapacityPerBucket);

    internal bool AddOrUpdate(PeerState peerState)
    {
        using var _ = _lock.WriteScope();
        return AddOrUpdateInternal(peerState);
    }

    private void AddInternal(Peer peer)
    {
        if (peer.Address == owner)
        {
            throw new ArgumentException("Cannot add self address to the routing table.", nameof(peer));
        }

        _buckets[peer.Address].Add(new PeerState { Peer = peer, LastUpdated = DateTimeOffset.UtcNow });
        _addedSubject.OnNext(peer);
    }

    private bool AddOrUpdateInternal(PeerState peerState)
    {
        var address = peerState.Address;
        if (owner == address)
        {
            throw new ArgumentException("Cannot add self address to the routing table.", nameof(peerState));
        }

        var contains = _buckets[address].Contains(address);
        if (_buckets[address].AddOrUpdate(peerState))
        {
            if (contains)
            {
                _updatedSubject.OnNext(peerState.Peer);
            }
            else
            {
                _addedSubject.OnNext(peerState.Peer);
            }

            return true;
        }

        return false;
    }

    private bool RemoveInternal(Peer peer)
    {
        var address = peer.Address;
        if (address == owner)
        {
            return false;
        }

        var bucket = _buckets[address];
        if (bucket.TryGetValue(address, out var peerState) && peerState.Peer == peer && bucket.Remove(address))
        {
            _removedSubject.OnNext(peer);
            return true;
        }

        return false;
    }
}
