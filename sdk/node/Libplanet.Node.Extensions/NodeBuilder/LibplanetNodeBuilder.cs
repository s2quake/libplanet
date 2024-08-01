using Libplanet.Blockchain.Renderers;
using Libplanet.Node.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Libplanet.Node.Extensions.NodeBuilder;

public class LibplanetNodeBuilder : ILibplanetNodeBuilder
{
    internal LibplanetNodeBuilder(IServiceCollection services)
    {
        Services = services;
        Services.AddSingleton<PolicyService>();
        Services.AddSingleton<BlockChainService>();
        Services.AddSingleton<IBlockChainService, BlockChainService>();
        Services.AddSingleton<IReadChainService, ReadChainService>();
        Services.AddSingleton<TransactionService>();

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var query = from assembly in AppDomain.CurrentDomain.GetAssemblies()
                    from type in assembly.GetTypes()
                    where typeof(IActionLoaderProvider).IsAssignableFrom(type)
                    where type.IsClass && !type.IsAbstract
                    select type;

        foreach (var type in query)
        {
            Services.AddTransient(typeof(IActionLoaderProvider), type);
        }
    }

    public IServiceCollection Services
    {
        get;
    }

    public ILibplanetNodeBuilder WithSolo()
    {
        Services.AddHostedService<SoloProposeService>();
        return this;
    }

    public ILibplanetNodeBuilder WithSwarm()
    {
        Services.AddHostedService<NodeService>();
        Services.AddSingleton<INodeService, NodeService>();
        return this;
    }

    public ILibplanetNodeBuilder WithValidate() =>
        this;

    public ILibplanetNodeBuilder WithSeed()
    {
        Services.AddSingleton<IBlocksyncSeedService, BlocksyncSeedService>();
        Services.AddSingleton<IConsensusSeedService, ConsensusSeedService>();
        Services.AddHostedService<BlocksyncSeedService>();
        Services.AddHostedService<ConsensusSeedService>();
        Services.AddHostedService<NodeService>();
        return this;
    }
}
