using JSSoft.Commands;
using Libplanet.Types;

namespace Libplanet.Commands.Keys;

[CommandSummary("Derive public key and address from private key.")]
[CommandStaticProperty(typeof(FormatProperties))]
public sealed class DeriveKeyCommand(KeyCommand keyCommand)
    : CommandBase(keyCommand, "derive")
{
    [CommandPropertyRequired]
    [CommandSummary("Outputs only the private key in hex format.")]
    public string Key { get; set; } = string.Empty;

    [CommandPropertySwitch("public-key", 'P')]
    [CommandSummary("Derive from a public key instead of a private key.")]
    public bool IsPublicKey { get; set; }

    protected override void OnExecute()
    {
        var publicKey = IsPublicKey ? PublicKey.Parse(Key) : PrivateKey.Parse(Key).PublicKey;
        var info = new Dictionary<string, string>
        {
            ["address"] = publicKey.Address.ToString(),
            ["publicKey"] = publicKey.ToString(),
        };
        FormatProperties.WriteLine(Out, info);
    }

}
