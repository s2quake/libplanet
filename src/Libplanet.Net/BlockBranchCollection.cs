using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net;

public sealed class BlockBranchCollection : IEnumerable<BlockBranch>
{
    private readonly ConcurrentDictionary<BlockHash, BlockBranch> _table = new();

    public int Count => _table.Count;

    public BlockBranch this[BlockHash blockHash] => _table[blockHash];

    public void Add(BlockHash blockHash, BlockBranch branch)
    {
        if (!_table.TryAdd(blockHash, branch))
        {
            throw new ArgumentException("A block branch with the same hash already exists.", nameof(blockHash));
        }
    }

    public bool TryGetValue(BlockHash blockHash, [MaybeNullWhen(false)] out BlockBranch value)
        => _table.TryGetValue(blockHash, out value);

    public bool Remove(BlockHash blockHash) => _table.TryRemove(blockHash, out _);

    public void RemoveAll(Func<BlockHash, bool> predicate)
    {
        foreach (var blockHash in _table.Keys.ToArray())
        {
            if (!predicate(blockHash))
            {
                Remove(blockHash);
            }
        }
    }

    public IEnumerator<BlockBranch> GetEnumerator() => _table.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
