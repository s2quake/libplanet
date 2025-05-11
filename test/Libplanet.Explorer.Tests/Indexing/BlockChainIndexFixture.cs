using System.Threading;
using Libplanet.Explorer.Indexing;

namespace Libplanet.Explorer.Tests.Indexing;

public abstract class BlockChainIndexFixture : IBlockChainIndexFixture
{
    public IBlockChainIndex Index { get; }

    protected BlockChainIndexFixture(Libplanet.Store.Store store, IBlockChainIndex index)
    {
        Index = index;
        Index.SynchronizeAsync(store, CancellationToken.None).GetAwaiter().GetResult();
    }

    public abstract IBlockChainIndex CreateEphemeralIndexInstance();
}
