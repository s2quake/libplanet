using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Types;

namespace Libplanet.Net;

public sealed class BlockBranchCollection(Blockchain blockchain) : IEnumerable<BlockBranch>
{
    private readonly ConcurrentDictionary<BlockHeader, BlockBranch> _branchByHeader = new();

    public int Count => _branchByHeader.Count;

    internal Blockchain Blockchain => blockchain;

    public BlockBranch this[BlockHeader blockHeader] => _branchByHeader[blockHeader];

    public void Add(BlockHeader blockHeader, BlockBranch branch)
    {
        if (!_branchByHeader.TryAdd(blockHeader, branch))
        {
            throw new ArgumentException("A block branch with the same header already exists.", nameof(blockHeader));
        }
    }

    public bool TryGetValue(BlockHeader blockHeader, [MaybeNullWhen(false)] out BlockBranch value)
        => _branchByHeader.TryGetValue(blockHeader, out value);

    public bool Remove(BlockHeader blockHeader) => _branchByHeader.TryRemove(blockHeader, out _);

    public void Prune()
    {
        var blockHeaders = _branchByHeader.Keys.ToArray();
        foreach (var blockHeader in blockHeaders)
        {
            if (blockHeader.Height <= blockchain.Tip.Height)
            {
                Remove(blockHeader);
            }
        }
    }

    public void Clear() => _branchByHeader.Clear();

    public IEnumerator<BlockBranch> GetEnumerator() => _branchByHeader.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
