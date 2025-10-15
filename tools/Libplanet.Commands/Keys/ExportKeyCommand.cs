using JSSoft.Commands;
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

    [CommandPropertySwitch("web3")]
    [CommandSummary("Outputs the key in Web3 Secret Storage Formatted json.")]
    [CommandPropertyExclusion(nameof(FormatProperties.Json))]
    [CommandPropertyExclusion(nameof(Pure))]
    public bool Web3 { get; set; }

    [CommandProperty]
    [CommandSummary("Path to key store")]
    public string StorePath { get; set; } = string.Empty;

    protected override void OnExecute()
    {
        var keyId = Guid.Parse(KeyId);
        var keyStore = StorePath == string.Empty ? Web3KeyStore.Default : new Web3KeyStore(StorePath);
        if (!keyStore.Contains(keyId))
        {
            throw new KeyNotFoundException($"The key {KeyId} does not exist.");
        }

        var passphrase = PassphraseProperties.GetPassphrase(keyId);
        var privateKey = keyStore.Get(keyId, passphrase);

        if (Pure)
        {
            Out.WriteLine(ByteUtility.Hex(privateKey.Bytes));
        }
        else if (Web3)
        {
            Out.WriteLine(OutputUtility.ToColorizedJsonString(keyStore.GetJson(keyId)));
        }
        else
        {
            var keyInfo = new KeyInfo
            {
                KeyId = keyId.ToString(),
                PrivateKey = ByteUtility.Hex(privateKey.Bytes),
                Address = privateKey.Address.ToString(),
                PublicKey = privateKey.PublicKey.ToString(),
            };

            FormatProperties.WriteLine(Out, keyInfo);
        }
    }
}
