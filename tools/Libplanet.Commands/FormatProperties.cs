using JSSoft.Commands;

namespace Libplanet.Commands;

public static class FormatProperties
{
    [CommandPropertySwitch("json")]
    [CommandSummary("Outputs in JSON format.")]
    public static bool Json { get; set; }
}
