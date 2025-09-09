using Libplanet.Node.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Libplanet.Node.Extensions.NodeBuilder;

public class LibplanetNodeBuilder : ILibplanetNodeBuilder
{
    private readonly List<string> _scopeList = [string.Empty];

    internal LibplanetNodeBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IServiceCollection Services { get; }

    public string[] Scopes => [.. _scopeList];

    public ILibplanetNodeBuilder WithValidator()
    {
        Services.AddSingleton<ValidatorService>()
            .AddSingleton(s => (IValidatorService)s.GetRequiredService<ValidatorService>())
            .AddHostedService(s => s.GetRequiredService<ValidatorService>());
        _scopeList.Add("Validator");
        return this;
    }
}
