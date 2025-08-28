using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Libplanet.Data;
using Libplanet.Types;

namespace Libplanet;

public sealed class BlockCommitCollection : IReadOnlyDictionary<BlockHash, BlockCommit>
{
    private readonly BlockDigestIndex _blockDigests;
    private readonly BlockCommitIndex _blockCommits;
    private readonly BlockHashIndex _blockHashes;

    internal BlockCommitCollection(Repository repository)
    {
        _blockDigests = repository.BlockDigests;
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

    public BlockCommit this[int height] => this[_blockHashes[height]];

    public BlockCommit this[BlockHash blockHash]
    {
        get
        {
            if (_blockDigests.ContainsKey(blockHash))
            {
                if (_blockCommits.TryGetValue(blockHash, out var commit))
                {
                    return commit;
                }

                return default;
            }

            throw new KeyNotFoundException(
                $"Block commit for {blockHash} not found. " +
                "Use `TryGetValue` to check existence without throwing an exception.");
        }
    }

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

    public bool ContainsKey(BlockHash blockHash) => _blockDigests.ContainsKey(blockHash);

    public bool TryGetValue(int height, [MaybeNullWhen(false)] out BlockCommit value)
        => TryGetValue(_blockHashes[height], out value);

    public bool TryGetValue(BlockHash blockHash, [MaybeNullWhen(false)] out BlockCommit value)
    {
        if (_blockDigests.ContainsKey(blockHash))
        {
            if (_blockCommits.TryGetValue(blockHash, out value))
            {
                return true;
            }

            value = default;
            return true;
        }

        value = default;
        return false;
    }

    public IEnumerator<KeyValuePair<BlockHash, BlockCommit>> GetEnumerator()
    {
        foreach (var key in Keys)
        {
            yield return new KeyValuePair<BlockHash, BlockCommit>(key, this[key]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
