using JSSoft.Commands;
using Libplanet.Types;

namespace Libplanet.Commands.Keys;

[CommandSummary("Generate a new private key without storing it.")]
[CommandStaticProperty(typeof(FormatProperties))]
public sealed class GenerateKeyCommand(KeyCommand keyCommand)
    : CommandBase(keyCommand, "generate", aliases: ["gen"])
{
    [CommandPropertySwitch]
    [CommandSummary("Outputs only the private key in hex format.")]
    [CommandPropertyExclusion(nameof(FormatProperties.Json))]
    public bool Pure { get; set; }

    protected override void OnExecute()
    {
        var privateKey = new PrivateKey();

        if (Pure)
        {
            Out.WriteLine(ByteUtility.Hex(privateKey.Bytes));
        }
        else
        {
            var info = new Dictionary<string, string>
            {
                ["privateKey"] = ByteUtility.Hex(privateKey.Bytes),
                ["address"] = privateKey.Address.ToString(),
                ["publicKey"] = privateKey.PublicKey.ToString(),
            };

            FormatProperties.WriteLine(Out, info);
        }
    }
}
