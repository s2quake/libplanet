using Microsoft.Extensions.Logging;

namespace Libplanet.Node.Options;

internal sealed class RepositoryOptionsConfigurator(ILogger<RepositoryOptionsConfigurator> logger)
    : OptionsConfiguratorBase<RepositoryOptions>
{
    protected override void OnConfigure(RepositoryOptions options)
    {
        if (options.Type == RepositoryType.InMemory)
        {
            if (options.Path != string.Empty)
            {
                options.Path = string.Empty;
                logger.LogWarning(
                    "RootPath is ignored because StoreType is {Memory}.", RepositoryType.InMemory);
            }

        }
        else
        {
            if (options.Path == string.Empty)
            {
                options.Path = RepositoryOptions.DefaultRootPath;
                logger.LogDebug(
                    "RootPath is not set. Use the default path: {RootPath}", options.Path);
            }

            options.Path = Path.GetFullPath(options.Path);
        }
    }
}
