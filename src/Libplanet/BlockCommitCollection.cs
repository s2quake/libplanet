using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Data;
using Libplanet.Types;

namespace Libplanet;

public sealed class BlockCommitCollection : IReadOnlyDictionary<BlockHash, BlockCommit>
{
    private readonly BlockCommitIndex _blockCommits;
    private readonly BlockHashIndex _blockHashes;

    internal BlockCommitCollection(Repository repository)
    {
        _blockCommits = repository.BlockCommits;
        _blockHashes = repository.BlockHashes;
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

    public BlockCommit this[Index index] => this[_blockHashes[index]];

    public BlockCommit this[BlockHash blockHash] => _blockCommits[blockHash];

    public IEnumerable<BlockCommit> this[Range range]
    {
        get
        {
            foreach (var blockHash in _blockHashes[range])
            {
                yield return this[blockHash];
            }
        }
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
