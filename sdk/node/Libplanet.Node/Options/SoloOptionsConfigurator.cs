using Libplanet.Types;
using Libplanet.Types;
using Microsoft.Extensions.Logging;

namespace Libplanet.Node.Options;

internal sealed class SoloOptionsConfigurator(
    ILogger<SoloOptionsConfigurator> logger)
    : OptionsConfiguratorBase<SoloOptions>
{
    protected override void OnConfigure(SoloOptions options)
    {
        if (options.PrivateKey == string.Empty)
        {
            options.PrivateKey = ByteUtility.Hex(new PrivateKey().Bytes);
            logger.LogWarning(
                "Node's private key is not set. A new private key is generated: {PrivateKey}",
                options.PrivateKey);
        }
    }
}
