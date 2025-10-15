using System.IO;
using JSSoft.Commands;
using Libplanet.Serialization;
using Libplanet.Serialization.Json;
using Libplanet.Serialization.Yaml;
using Libplanet.Types;

namespace Libplanet.Commands;

public static class ModelFormatProperties
{
    [CommandPropertySwitch]
    public static bool Pretty { get; set; }

    [CommandPropertySwitch("json")]
    [CommandSummary("Outputs in JSON format.")]
    [CommandPropertyDependency(nameof(Pretty))]
    public static bool Json { get; set; }

    public static void WriteLine(TextWriter textWriter, object obj)
    {
        if (Pretty)
        {
            if (Json)
            {
                var json = ModelJsonSerializer.Serialize(obj);
                textWriter.WriteLine(OutputUtility.ToColorizedJsonString(json));
            }
            else
            {
                var yaml = ModelYamlSerializer.Serialize(obj);
                textWriter.WriteLine(OutputUtility.ToColorizedYamlString(yaml));
            }
        }
        else
        {
            var bytes = ModelSerializer.Serialize(obj);
            textWriter.WriteLine(ByteUtility.Hex(bytes));
        }
    }

    public static void Save(string path, object obj, bool force)
    {
        if (File.Exists(path) && !force)
        {
            throw new IOException($"File already exists: {path}");
        }

        if (Pretty)
        {
            if (Json)
            {
                var json = ModelJsonSerializer.Serialize(obj);
                File.WriteAllText(path, json);
            }
            else
            {
                var yaml = ModelYamlSerializer.Serialize(obj);
                File.WriteAllText(path, yaml);
            }
        }
        else
        {
            var bytes = ModelSerializer.Serialize(obj);
            File.WriteAllBytes(path, bytes);
        }
    }
}
