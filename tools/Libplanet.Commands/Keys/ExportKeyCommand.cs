using JSSoft.Commands;
using Libplanet.Commands.Extensions;
using Libplanet.KeyStore;
using Libplanet.Types;

namespace Libplanet.Commands.Keys;

[CommandSummary("Export a raw private key (or public key).")]
[CommandStaticProperty(typeof(PassphraseProperties))]
[CommandStaticProperty(typeof(FormatProperties))]
public sealed class ExportKeyCommand(KeyCommand keyCommand)
    : CommandBase(keyCommand, "export")
{
    [CommandPropertyRequired]
    [CommandSummary("A key UUID to export.")]
    public string KeyId { get; set; } = string.Empty;

    [CommandPropertySwitch]
    [CommandSummary("Outputs only the private key in hex format.")]
    [CommandPropertyExclusion(nameof(FormatProperties.Json))]
    public bool Pure { get; set; }

    [CommandPropertySwitch("web3-json")]
    [CommandSummary("Outputs the key in Web3 Secret Storage Formatted json.")]
    [CommandPropertyExclusion(nameof(FormatProperties.Json))]
    [CommandPropertyExclusion(nameof(Pure))]
    public bool Web3Json { get; set; }

    [CommandProperty]
    [CommandSummary("Path to key store")]
    public string StorePath { get; set; } = string.Empty;

    protected override void OnExecute()
    {
        var keyId = Guid.Parse(KeyId);
        var keyStore = StorePath == string.Empty ? Web3KeyStore.DefaultKeyStore : new Web3KeyStore(StorePath);
        var ppk = keyStore.Get(keyId);
        var passphrase = PassphraseProperties.GetPassphrase(keyId);

        var privateKey = ppk.Unprotect(passphrase);

        if (Pure)
        {
            Out.WriteLine(ByteUtility.Hex(privateKey.Bytes));
        }
        else if (Web3Json)
        {
            Out.WriteLineAsJson((object)ppk.ToDynamic(keyId));
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
