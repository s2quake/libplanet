using Libplanet.Types;
using Libplanet.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Libplanet.Node.Options;

internal sealed class GenesisOptionsConfigurator(
    IOptions<SwarmOptions> nodeOptions, ILogger<GenesisOptionsConfigurator> logger)
    : OptionsConfiguratorBase<GenesisOptions>
{
    protected override void OnConfigure(GenesisOptions options)
    {
        if (options.GenesisBlockPath == string.Empty &&
            options.GenesisConfigurationPath == string.Empty)
        {
            if (options.GenesisKey == string.Empty)
            {
                var privateKey = new PrivateKey();
                options.GenesisKey = ByteUtility.Hex(privateKey.Bytes);
                logger.LogWarning(
                    "Genesis key is not set. A new private key is generated:{PrivateKey}",
                    options.GenesisKey);
            }

            if (options.Validators.Length == 0)
            {
                var privateKey = PrivateKey.Parse(nodeOptions.Value.PrivateKey);
                options.Validators = [privateKey.PublicKey.ToString()];
                logger.LogWarning(
                    "Validators are not set. Use the node's private key as a validator.");
            }
        }
    }
}
