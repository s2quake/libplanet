using JSSoft.Commands;
using Libplanet.Types;

namespace Libplanet.Commands.Keys;

[CommandSummary("Create a new private key.")]
[CommandStaticProperty(typeof(PassphraseProperties))]
[CommandStaticProperty(typeof(FormatProperties))]
public sealed class CreateKeyCommand(KeyCommand keyCommand)
    : CommandBase(keyCommand, "create")
{
    [CommandPropertySwitch]
    [CommandSummary("Do not add to the key store, but only show the created key.")]
    public bool DryRun { get; set; }

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
    [CommandPropertyExclusion(nameof(DryRun))]
    public string StorePath { get; set; } = string.Empty;

    protected override void OnExecute()
    {
        var passphrase = PassphraseProperties.GetPassphrase();
        var privateKey = new PrivateKey();
        var keyStore = StorePath == string.Empty ? Web3KeyStore.Default : new Web3KeyStore(StorePath);
        var keyId = DryRun ? Guid.NewGuid() : keyStore.Add(privateKey, passphrase);

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
