using JSSoft.Commands;
using Libplanet.Types;

namespace Libplanet.Commands.Keys;

[CommandSummary("Import a raw private key or Web3 Secret Storage.")]
[CommandStaticProperty(typeof(PassphraseProperties))]
[CommandStaticProperty(typeof(FormatProperties))]
public sealed class ImportKeyCommand(KeyCommand keyCommand)
    : CommandBase(keyCommand, "import")
{
    [CommandPropertyRequired]
    [CommandSummary("A raw private key in hexadecimal string to import.")]
    public string Key { get; set; } = string.Empty;

    [CommandPropertySwitch("web3")]
    [CommandSummary("Use the given key value as Web3 Secret Storage Formatted json.")]
    [CommandPropertyExclusion(nameof(PassphraseProperties.Passphrase))]
    [CommandPropertyExclusion(nameof(PassphraseProperties.PassphraseFile))]
    public bool KeyAsJson { get; set; }

    [CommandProperty]
    [CommandSummary("Path to key store")]
    public string StorePath { get; set; } = string.Empty;

    protected override void OnExecute()
    {
        var keyStore = StorePath == string.Empty ? Web3KeyStore.Default : new Web3KeyStore(StorePath);
        var keyId = KeyAsJson
            ? keyStore.Add(json: System.IO.File.ReadAllText(Key))
            : keyStore.Add(PrivateKey.Parse(Key), PassphraseProperties.GetPassphrase());
        Out.WriteLine($"{keyId}");
    }
}
