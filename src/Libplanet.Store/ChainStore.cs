using System.Collections;
using System.Diagnostics.CodeAnalysis;
using BitFaster.Caching;
using BitFaster.Caching.Lru;
using Libplanet.Types.Blocks;

namespace Libplanet.Store;

public sealed class ChainStore(Store store)
    : IReadOnlyDictionary<Guid, Chain>
{
    public Chain this[Guid key] => throw new NotImplementedException();

    public IEnumerable<Guid> Keys => throw new NotImplementedException();

    public IEnumerable<Chain> Values => throw new NotImplementedException();

    public int Count => throw new NotImplementedException();

    public bool ContainsKey(Guid key)
    {
        throw new NotImplementedException();
    }

    public IEnumerator<KeyValuePair<Guid, Chain>> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    public bool TryGetValue(Guid key, [MaybeNullWhen(false)] out Chain value)
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
