using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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

    [Description("The key of the genesis block.")]
    public string GenesisKey { get; set; } = string.Empty;

    [Description("The hash of the genesis block.")]
    [RegularExpression("^(?:[0-9a-fA-F]{64})$")]
    public string[] Validators { get; set; } = [];

    [Description("The timestamp of the genesis block.")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.MinValue;
}
