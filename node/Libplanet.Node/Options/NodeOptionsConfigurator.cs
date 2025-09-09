using Libplanet.Types;
using Microsoft.Extensions.Logging;

namespace Libplanet.Node.Options;

internal sealed class NodeOptionsConfigurator(
    ILogger<NodeOptionsConfigurator> logger)
    : OptionsConfiguratorBase<NodeOptions>
{
    protected override void OnConfigure(NodeOptions options)
    {
        if (options.PrivateKey == string.Empty)
        {
            options.PrivateKey = ByteUtility.Hex(new PrivateKey().Bytes);
            logger.LogWarning(
                "Node's private key is not set. A new private key is generated: {PrivateKey}",
                options.PrivateKey);
        }

        if (options.EndPoint == string.Empty)
        {
            options.EndPoint = EndPointUtility.ToString(EndPointUtility.Next());
            logger.LogWarning(
                "Node's endpoint is not set. A new endpoint is generated: {EndPoint}",
                options.EndPoint);
        }
    }
}
