using Libplanet.Node.Extensions.NodeBuilder;
using Libplanet.Node.Options;
using Libplanet.Node.Options.Schema;
using Libplanet.Node.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Extensions;

public static class LibplanetServicesExtensions
{
    public static ILibplanetNodeBuilder AddLibplanetNode(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        SynchronizationContext.SetSynchronizationContext(SynchronizationContext.Current ?? new());
        services.AddSingleton(SynchronizationContext.Current!);
        services.AddOptions<GenesisOptions>()
                .Bind(configuration.GetSection(GenesisOptions.Position));
        services.AddSingleton<IConfigureOptions<GenesisOptions>, GenesisOptionsConfigurator>();
        services.AddSingleton<IValidateOptions<GenesisOptions>, GenesisOptionsValidator>();

        services.AddOptions<RepositoryOptions>()
                .Bind(configuration.GetSection(RepositoryOptions.Position));
        services.AddSingleton<IConfigureOptions<RepositoryOptions>, RepositoryOptionsConfigurator>();

        services.AddOptions<NodeOptions>()
                .Bind(configuration.GetSection(NodeOptions.Position));
        services.AddSingleton<IConfigureOptions<NodeOptions>, NodeOptionsConfigurator>();
        services.AddSingleton<IValidateOptions<NodeOptions>, NodeOptionsValidator>();

        services.AddOptions<ValidatorOptions>()
                .Bind(configuration.GetSection(ValidatorOptions.Position));
        services.AddSingleton<IConfigureOptions<ValidatorOptions>, ValidatorOptionsConfigurator>();
        services.AddSingleton<IValidateOptions<ValidatorOptions>, ValidatorOptionsValidator>();

        services.AddSingleton<RepositoryService>();
        services.AddSingleton(s => (IRepositoryService)s.GetRequiredService<RepositoryService>());
        services.AddSingleton<BlockchainService>()
            .AddSingleton(s => (IBlockchainService)s.GetRequiredService<BlockchainService>());

        var nodeBuilder = new LibplanetNodeBuilder(services);


        if (configuration.IsOptionsEnabled(ValidatorOptions.Position))
        {
            nodeBuilder.WithValidator();
        }

        return nodeBuilder;
    }

    public static IApplicationBuilder MapSchemaBuilder(this IApplicationBuilder app, string pattern)
    {
        app.UseRouting();
        app.UseEndpoints(endPoint =>
        {
            string? schema = null;
            endPoint.MapGet(pattern, async () =>
            {
                schema ??= await OptionsSchemaBuilder.GetSchemaAsync(default);
                return schema;
            });
        });

        return app;
    }

    public static bool IsOptionsEnabled(
        this IConfiguration configuration, string name)
        => configuration.GetValue<bool>($"{name}:IsEnabled");

    public static bool IsOptionsEnabled(
        this IConfiguration configuration, string name, string propertyName)
    {
        var key = $"{name}:{propertyName}";
        return configuration.GetValue<bool>(key);
    }
}
