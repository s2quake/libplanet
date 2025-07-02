using System.Collections.Concurrent;
using Libplanet.Types;

namespace Libplanet.Net;

public sealed class BlockCandidateTable
{
    private readonly ConcurrentDictionary<BlockExcerpt, ImmutableSortedDictionary<Block, BlockCommit>> _table = new();

    public long Count => _table.Count;

    public void Add(BlockExcerpt blockHeader, ImmutableSortedDictionary<Block, BlockCommit> branch)
    {
        if (_table.ContainsKey(blockHeader))
        {
            return;
        }

        _table.TryAdd(blockHeader, branch);
    }

    public ImmutableSortedDictionary<Block, BlockCommit> GetCurrentRoundCandidate(BlockExcerpt thisRoundTip)
    {
        return _table.TryGetValue(thisRoundTip, out var branch)
            ? branch : ImmutableSortedDictionary<Block, BlockCommit>.Empty;
    }

    public bool TryRemove(BlockExcerpt header)
    {
        return _table.TryRemove(header, out _);
    }

    public void Cleanup(Func<BlockExcerpt, bool> predicate)
    {
        foreach (var blockHeader in _table.Keys)
        {
            if (!predicate(blockHeader))
            {
                TryRemove(blockHeader);
            }
        }
    }
}
