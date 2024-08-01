using Libplanet.Action.Loader;
using Libplanet.Node.DependencyInjection;
using Libplanet.Node.Extensions.NodeBuilder;
using Libplanet.Node.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Extensions;

public static class LibplanetServicesExtensions
{
    public static ILibplanetNodeBuilder AddLibplanetNode(
        this IServiceCollection services,
        Action<LibplanetOption> configure)
    {
        services.Configure(configure);
        return AddLibplanetNode(services);
    }

    public static ILibplanetNodeBuilder AddLibplanetNode(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<StoreOptions>(configuration.GetSection(StoreOptions.Position));
        services.Configure<SoloProposeOption>(configuration.GetSection(SoloProposeOption.Position));
        services.Configure<GenesisOptions>(configuration.GetSection(GenesisOptions.Position));
        services.Configure<SeedOptions>(
            SeedOptions.BlocksyncSeed, configuration.GetSection(SeedOptions.BlocksyncSeed));
        services.Configure<SeedOptions>(
            SeedOptions.ConsensusSeed, configuration.GetSection(SeedOptions.ConsensusSeed));
        services.Configure<NodeOptions>(configuration.GetSection(NodeOptions.Position));

        var types = ServiceUtility.GetTypes(typeof(SingletonAttribute), inherit: true);
        foreach (var type in types)
        {
            var serviceTypes = GetAttributes(type).Select(item => item.ServiceType);
            foreach (var serviceType in serviceTypes)
            {
                services.AddSingleton(serviceType ?? type, type);
            }

            static IEnumerable<SingletonAttribute> GetAttributes(Type type)
                => Attribute.GetCustomAttributes(type, typeof(SingletonAttribute))
                    .OfType<SingletonAttribute>();
        }

        return AddLibplanetNode(services);
    }

    private static ILibplanetNodeBuilder AddLibplanetNode(this IServiceCollection services)
        => new LibplanetNodeBuilder(services);
}
