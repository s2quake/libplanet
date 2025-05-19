using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Store;
using Libplanet.Types.Blocks;

namespace Libplanet.Blockchain;

public sealed class BlockCommitCollection : IReadOnlyDictionary<BlockHash, BlockCommit>
{
    private readonly BlockCommitStore _blockCommits;
    private readonly BlockHashStore _blockHashes;

    internal BlockCommitCollection(Repository repository, Guid chainId)
    {
        _blockCommits = repository.BlockCommits;
        _blockHashes = repository.Chains[chainId].BlockHashes;
    }

    public IEnumerable<BlockHash> Keys
    {
        get
        {
            for (var i = 0; i < _blockHashes.Count; i++)
            {
                yield return _blockHashes[i];
            }
        }
    }

    public IEnumerable<BlockCommit> Values
    {
        get
        {
            for (var i = 0; i < _blockHashes.Count; i++)
            {
                var blockHash = _blockHashes[i];
                yield return this[blockHash];
            }
        }
    }

    public int Count => _blockHashes.Count;

    public BlockCommit this[int height] => this[_blockHashes[height]];

    public BlockCommit this[Index index]
    {
        get
        {
            if (index.IsFromEnd)
            {
                return this[Count - index.Value];
            }

            if (index.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return this[index.Value];
        }
    }

    public BlockCommit this[BlockHash blockHash] => _blockCommits[blockHash];

    public IEnumerable<BlockHash> IterateIndexes(int offset = 0, int? limit = null)
    {
        return _blockHashes.IterateHeights(offset, limit);
    }

    public bool ContainsKey(BlockHash blockHash) => _blockCommits.ContainsKey(blockHash);

    public bool TryGetValue(int height, [MaybeNullWhen(false)] out BlockCommit value)
        => TryGetValue(_blockHashes[height], out value);

    public bool TryGetValue(BlockHash blockHash, [MaybeNullWhen(false)] out BlockCommit value)
        => _blockCommits.TryGetValue(blockHash, out value);

    public IEnumerator<KeyValuePair<BlockHash, BlockCommit>> GetEnumerator()
    {
        foreach (var key in Keys)
        {
            yield return new KeyValuePair<BlockHash, BlockCommit>(key, this[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
