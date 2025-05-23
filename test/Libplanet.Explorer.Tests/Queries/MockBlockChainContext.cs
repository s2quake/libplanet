using Libplanet.Blockchain;
using Libplanet.Explorer.Indexing;
using Libplanet.Explorer.Interfaces;
using Libplanet.Net;

namespace Libplanet.Explorer.Tests.Queries;

public class MockBlockChainContext : IBlockChainContext
{
    public bool Preloaded => true;

    public BlockChain BlockChain { get; }

    public Libplanet.Data.Repository Store { get; }

    public Swarm Swarm { get; }

    public IBlockChainIndex Index { get; protected init; }

    public MockBlockChainContext(BlockChain chain)
    {
        BlockChain = chain;
        // Store = BlockChain._repository;
    }

    public MockBlockChainContext(Libplanet.Data.Repository store)
    {
        Store = store;
    }

    public MockBlockChainContext()
    {
    }
}
