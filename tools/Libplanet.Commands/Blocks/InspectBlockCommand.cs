using System.IO;
using JSSoft.Commands;
using Libplanet.Serialization;
using Libplanet.Serialization.Json;
using Libplanet.Serialization.Yaml;
using Libplanet.Types;

namespace Libplanet.Commands.Blocks;

[CommandSummary("Inspect a block.")]
[CommandStaticProperty(typeof(FormatProperties))]
public sealed class InspectBlockCommand(BlockCommand blockCommand)
    : CommandBase(blockCommand, "inspect")
{
    [CommandPropertyRequired]
    public string BlockPath { get; set; } = string.Empty;

    [CommandPropertySwitch("from-json")]
    [CommandPropertyExclusion(nameof(FromYaml))]
    public bool FromJson { get; set; }

    [CommandPropertySwitch("from-yaml")]
    [CommandPropertyExclusion(nameof(FromJson))]
    public bool FromYaml { get; set; }

    protected override void OnExecute()
    {
        var block = LoadBlock(BlockPath);
        FormatProperties.WriteLine(Out, block);
    }

    private Block LoadBlock(string path)
    {
        if (FromJson)
        {
            return ModelJsonSerializer.Deserialize<Block>(File.ReadAllText(path));
        }
        else if (FromYaml)
        {
            return ModelYamlSerializer.Deserialize<Block>(File.ReadAllText(path));
        }
        else
        {
            var bytes = File.ReadAllBytes(path);
            return ModelSerializer.Deserialize<Block>(bytes);
        }
    }
}
