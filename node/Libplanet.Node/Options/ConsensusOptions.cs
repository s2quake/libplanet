using System.ComponentModel.DataAnnotations;

namespace Libplanet.Node.Options;

[Options(Position)]
public sealed class ConsensusOptions : OptionsBase<ConsensusOptions>
{
    public const string Position = "Consensus";

    public bool IsEnabled { get; set; }

    [Range(0, 65535)]
    public int Port { get; set; }
}
