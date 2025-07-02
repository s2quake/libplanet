using System.Collections.Concurrent;
using Libplanet.Types;

namespace Libplanet.Net;

public sealed class BlockCandidateTable
{
    private readonly ConcurrentDictionary<BlockSummary, ImmutableSortedDictionary<Block, BlockCommit>> _table = new();

    public long Count => _table.Count;

    public void Add(BlockSummary blockHeader, ImmutableSortedDictionary<Block, BlockCommit> branch)
    {
        if (_table.ContainsKey(blockHeader))
        {
            return;
        }

        _table.TryAdd(blockHeader, branch);
    }

    public ImmutableSortedDictionary<Block, BlockCommit> GetCurrentRoundCandidate(BlockSummary thisRoundTip)
    {
        return _table.TryGetValue(thisRoundTip, out var branch)
            ? branch : ImmutableSortedDictionary<Block, BlockCommit>.Empty;
    }

    public bool TryRemove(BlockSummary header)
    {
        return _table.TryRemove(header, out _);
    }

    public void Cleanup(Func<BlockSummary, bool> predicate)
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
