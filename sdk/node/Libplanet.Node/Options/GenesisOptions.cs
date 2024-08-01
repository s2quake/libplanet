using Libplanet.Crypto;
using Libplanet.Net;

namespace Libplanet.Node.Options;

public sealed class GenesisOptions
{
    public const string Position = "Genesis";

    public static readonly PrivateKey AppProtocolKey
        = PrivateKey.FromString("2a15e7deaac09ce631e1faa184efadb175b6b90989cf1faed9dfc321ad1db5ac");

    public static readonly AppProtocolVersion AppProtocolVersion = AppProtocolVersion.Sign(
        AppProtocolKey, 1);

    public GenesisOptions()
    {
    }

    public string GenesisKey { get; set; } = string.Empty;

    public string[] Validators { get; set; } = [];

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.MinValue;
}
