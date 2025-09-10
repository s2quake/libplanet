using System.Collections;
using System.IO;
using JSSoft.Commands;
using Libplanet.Commands.Extensions;

namespace Libplanet.Commands;

public static class FormatProperties
{
    [CommandPropertySwitch("json")]
    [CommandSummary("Outputs in JSON format.")]
    public static bool Json { get; set; }

    public static void WriteLine(TextWriter textWriter, IDictionary dictionary)
    {
        if (Json)
        {
            textWriter.WriteLineAsJson(dictionary);
        }
        else
        {
            textWriter.WriteLine(dictionary: dictionary);
        }
    }
}
