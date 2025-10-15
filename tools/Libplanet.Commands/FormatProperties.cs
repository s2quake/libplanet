using System.IO;
using JSSoft.Commands;

namespace Libplanet.Commands;

public static class FormatProperties
{
    [CommandPropertySwitch("json")]
    [CommandSummary("Outputs in JSON format.")]
    public static bool Json { get; set; }

    public static void WriteLine(TextWriter textWriter, object obj)
    {
        if (Json)
        {
            OutputUtility.WriteLine(textWriter, obj, OutputType.Json);
        }
        else
        {
            OutputUtility.WriteLine(textWriter, obj, OutputType.Yaml);
        }
    }

    public static void WriteLine(TextWriter textWriter, string value)
    {
        if (Json)
        {
            OutputUtility.WriteLine(textWriter, value, OutputType.Json);
        }
        else
        {
            OutputUtility.WriteLine(textWriter, value, OutputType.Yaml);
        }
    }
}
