using System.IO;
using System.Text;
using JSSoft.Commands;
using Libplanet.Serialization;
using Libplanet.Types;

namespace Libplanet.Commands.Blocks;

[CommandSummary("Inspect a block.")]
[CommandStaticProperty(typeof(FormatProperties))]
public sealed class InspectBlockCommand(BlockCommand blockCommand)
    : CommandBase(blockCommand, "inspect")
{
    [CommandPropertyRequired]
    public string BlockPath { get; set; } = string.Empty;

    protected override void OnExecute()
    {
        var bytes = File.ReadAllBytes(BlockPath);
        var block = ModelSerializer.Deserialize<Block>(bytes);

        var sb = new StringBuilder()
            .AppendLine($"Version: {block.Version}")
            .AppendLine($"Hash: {block.BlockHash}")
            .AppendLine($"Height: {block.Height}")
            .AppendLine($"Timestamp: {block.Timestamp:O}")
            .AppendLine($"PreviousHash: {block.PreviousBlockHash}")
            .AppendLine($"StateRoot: {block.PreviousStateRootHash}");
        Out.WriteLine(sb.ToString());
    }
}
