using System.Collections;
using Libplanet.Data;
using Libplanet.Types;

namespace Libplanet;

public sealed class BlockHashCollection(Repository repository) : IEnumerable<BlockHash>
{
    private readonly BlockHashIndex _blockHashes = repository.BlockHashes;

    public int Count => _blockHashes.Count;

    public BlockHash this[int height] => _blockHashes[height];

    public IEnumerable<BlockHash> this[Range range] => _blockHashes[range];

    public bool Contains(int height) => _blockHashes.ContainsKey(height);

    public IEnumerator<BlockHash> GetEnumerator() => _blockHashes.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
