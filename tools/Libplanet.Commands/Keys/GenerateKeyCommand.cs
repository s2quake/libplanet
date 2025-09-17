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
    public bool Pure { get; set; }

    protected override void OnExecute()
    {
        var privateKey = new PrivateKey();
        object info = Pure
            ? ByteUtility.Hex(privateKey.Bytes)
            : new KeyInfo
            {
                PrivateKey = ByteUtility.Hex(privateKey.Bytes),
                Address = privateKey.Address.ToString(),
                PublicKey = privateKey.PublicKey.ToString(),
            };

        FormatProperties.WriteLine(Out, info);
    }
}
