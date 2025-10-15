using Microsoft.Extensions.Logging;

namespace Libplanet.Node.Options;

internal sealed class RepositoryOptionsConfigurator(ILogger<RepositoryOptionsConfigurator> logger)
    : OptionsConfiguratorBase<RepositoryOptions>
{
    protected override void OnConfigure(RepositoryOptions options)
    {
        if (options.Type == RepositoryType.Memory)
        {
            if (options.Path != string.Empty)
            {
                options.Path = string.Empty;
                logger.LogWarning(
                    "RootPath is ignored because RepositoryType is {Memory}.", RepositoryType.Memory);
            }
        }
        else
        {
            options.Path = Path.GetFullPath(options.Path);
        }
    }
}
