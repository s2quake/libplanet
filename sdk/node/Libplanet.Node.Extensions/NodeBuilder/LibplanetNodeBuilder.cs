using Libplanet.Node.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Libplanet.Node.Extensions.NodeBuilder;

public class LibplanetNodeBuilder : ILibplanetNodeBuilder
{
    private readonly IConfiguration _configuration;
    private readonly List<string> _scopeList = [string.Empty];

    internal LibplanetNodeBuilder(IServiceCollection services, IConfiguration configuration)
    {
        Services = services;
        _configuration = configuration;
        Services.AddSingletonsFromDomain();
    }

    public IServiceCollection Services { get; }

    public string[] Scopes => [.. _scopeList];

    public ILibplanetNodeBuilder WithSolo()
    {
        Services.AddHostedService<SoloProposeService>();
        return this;
    }

    public ILibplanetNodeBuilder WithNode()
    {
        Services.AddSingletonsFromDomain(scope: "Node");
        Services.AddOptionsFromDomain(_configuration, scope: "Node");
        _scopeList.Add("Node");
        return this;
    }

    public ILibplanetNodeBuilder WithValidate() =>
        this;

    public ILibplanetNodeBuilder WithSeed()
    {
        Services.AddSingletonsFromDomain(scope: "Seed");
        Services.AddOptionsFromDomain(_configuration, scope: "Seed");
        _scopeList.Add("Seed");
        return this;
    }
}
