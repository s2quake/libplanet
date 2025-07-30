using Libplanet.Types;
using Libplanet.Types.Crypto;
using Microsoft.Extensions.Logging;

namespace Libplanet.Node.Options;

public sealed class SeedOptionsConfigurator(
    ILogger<SeedOptionsConfigurator> logger)
    : ConfigureNamedOptionsBase<SeedOptions>
{
    private static readonly PrivateKey _defaultPrivateKey = new();

    protected override void OnConfigure(string? name, SeedOptions options)
    {
        if (name == SeedOptions.BlocksyncSeed)
        {
            if (options.PrivateKey == string.Empty)
            {
                options.PrivateKey = ByteUtility.Hex(_defaultPrivateKey.Bytes);
                logger.LogWarning(
                    "BlocksyncSeed's private key is not set. A new private key is generated: " +
                    "{PrivateKey}",
                    options.PrivateKey);
            }
        }
        else if (name == SeedOptions.ConsensusSeed)
        {
            if (options.PrivateKey == string.Empty)
            {
                options.PrivateKey = ByteUtility.Hex(_defaultPrivateKey.Bytes);
                logger.LogWarning(
                    "ConsensusSeed's private key is not set. A new private key is generated: " +
                    "{PrivateKey}",
                    options.PrivateKey);
            }
        }
        else
        {
            logger.LogWarning(
                "SeedOptionsConfigurator is called with an unexpected name: {Name}",
                name);
        }

        if (options.EndPoint == string.Empty)
        {
            options.EndPoint = EndPointUtility.ToString(EndPointUtility.Next());
            logger.LogWarning(
                "{Name}'s endpoint is not set. A new endpoint is generated: {EndPoint}",
                name,
                options.EndPoint);
        }
    }
}
