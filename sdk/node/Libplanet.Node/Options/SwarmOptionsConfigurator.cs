using Libplanet.Net;
using Libplanet.Types;
using Libplanet.Types.Crypto;
using Microsoft.Extensions.Logging;

namespace Libplanet.Node.Options;

internal sealed class SwarmOptionsConfigurator(
    ILogger<SwarmOptionsConfigurator> logger)
    : OptionsConfiguratorBase<SwarmOptions>
{
    protected override void OnConfigure(SwarmOptions options)
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

        if (options.AppProtocolVersion == string.Empty)
        {
            var privateKey = PrivateKey.Parse(options.PrivateKey);
            var version = 0;
            var protocol = new ProtocolMetadata
            {
                Version = version,
                Signer = privateKey.Address,
            }.Sign(privateKey);
            options.AppProtocolVersion = protocol.Token;
            logger.LogWarning(
                "SwarmOptions.AppProtocolVersion is not set. A new version is " +
                "generated: {AppProtocolVersion}",
                options.AppProtocolVersion);
        }
    }
}
