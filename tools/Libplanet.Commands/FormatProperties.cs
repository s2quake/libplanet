using System.IO;
using JSSoft.Commands;
using Libplanet.Commands.Extensions;

namespace Libplanet.Commands;

public static class FormatProperties
{
    private static readonly YamlDotNet.Serialization.ISerializer _serializer
        = new YamlDotNet.Serialization.SerializerBuilder()
            .ConfigureDefaultValuesHandling(YamlDotNet.Serialization.DefaultValuesHandling.OmitDefaults)
            .Build();

    [CommandPropertySwitch("json")]
    [CommandSummary("Outputs in JSON format.")]
    public static bool Json { get; set; }

    public static void WriteLine(TextWriter textWriter, object obj)
    {
        if (Json)
        {
            textWriter.WriteLineAsJson(obj);
        }
        else
        {
            _serializer.Serialize(textWriter, obj);
        }
    }
}
