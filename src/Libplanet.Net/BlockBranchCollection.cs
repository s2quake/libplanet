using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Libplanet.Types;
using Libplanet.Types.Threading;

namespace Libplanet.Net;

public sealed class BlockBranchCollection : IEnumerable<BlockBranch>, INotifyCollectionChanged
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly Dictionary<BlockHeader, BlockBranch> _branchByHeader = [];

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _branchByHeader.Count;
        }
    }

    public BlockBranch this[BlockHeader blockHeader]
    {
        get
        {
            using var _ = _lock.ReadScope();
            return _branchByHeader[blockHeader];
        }
    }

    public void Add(BlockHeader blockHeader, BlockBranch blockBranch)
    {
        using var _ = _lock.WriteScope();
        if (!_branchByHeader.TryAdd(blockHeader, blockBranch))
        {
            throw new ArgumentException("A block branch with the same header already exists.", nameof(blockHeader));
        }

        CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Add, blockBranch));
    }

    public bool TryGetValue(BlockHeader blockHeader, [MaybeNullWhen(false)] out BlockBranch value)
    {
        using var _ = _lock.ReadScope();
        return _branchByHeader.TryGetValue(blockHeader, out value);
    }

    public bool Remove(BlockHeader blockHeader)
    {
        using var _ = _lock.WriteScope();
        if (_branchByHeader.TryGetValue(blockHeader, out var blockBranch))
        {
            var removed = _branchByHeader.Remove(blockHeader);
            CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Remove, blockBranch));
            return removed;
        }

        return false;
    }

    public void Prune(Blockchain blockchain)
    {
        using var _ = _lock.WriteScope();
        var tipHeight = blockchain.Tip.Height;
        var blockBranches = _branchByHeader.Values.Where(item => item.Height > tipHeight);
        foreach (var blockBranch in blockBranches)
        {
            CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Remove, blockBranch));
            _branchByHeader.Remove(blockBranch.BlockHeader);
        }
    }

    public void Clear()
    {
        using var _ = _lock.WriteScope();
        _branchByHeader.Clear();
        CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
    }

    public BlockBranch[] Flush(Blockchain blockchain)
    {
        using var _ = _lock.WriteScope();
        var tipHeight = blockchain.Tip.Height;
        var items = _branchByHeader.Values.Where(Predicate).ToArray();
        _branchByHeader.Clear();
        CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
        return items;

        bool Predicate(BlockBranch item) => item.Height <= tipHeight;
    }

    public IEnumerator<BlockBranch> GetEnumerator()
    {
        using var _ = _lock.ReadScope();
        var items = _branchByHeader.Values.ToArray();
        return ((IEnumerable<BlockBranch>)items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
