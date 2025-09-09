using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Libplanet.Node.DataAnnotations;

namespace Libplanet.Node.Options;

[Options(Position)]
public sealed class ValidatorOptions : OptionsBase<ValidatorOptions>
{
    public const string Position = "Validator";

    public bool IsEnabled { get; set; }

    [Range(0, 65535)]
    public int Port { get; set; }

    [Peer]
    [Description("The endpoint of the node to consensus.")]
    public string ConsensusSeedPeer { get; set; } = string.Empty;
}
