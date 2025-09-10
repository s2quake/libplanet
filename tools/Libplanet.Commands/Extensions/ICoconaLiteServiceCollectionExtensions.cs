using System.IO;
using global::Cocona.Lite;
using Libplanet.Commands.Configuration;
using Libplanet.Commands.Services;
using Zio.FileSystems;

namespace Libplanet.Commands.Extensions;

public static class ICoconaLiteServiceCollectionExtensions
{
    public static void AddJsonConfigurationService(
        this ICoconaLiteServiceCollection services,
        string jsonConfigurationPath)
    {
        string jsonConfigurationDirectory = Directory.GetParent(jsonConfigurationPath)?.FullName
            ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(jsonConfigurationDirectory))
        {
            Directory.CreateDirectory(jsonConfigurationDirectory);
        }

        var pfs = new PhysicalFileSystem();
        var fileSystem = new SubFileSystem(
            pfs,
            pfs.ConvertPathFromInternal(jsonConfigurationDirectory),
            owned: true);
        services.AddTransient<IConfigurationService<ToolConfiguration>>(
            _ => new JsonConfigurationService(
                fileSystem,
                Path.GetFileName(jsonConfigurationPath)));
    }
}
